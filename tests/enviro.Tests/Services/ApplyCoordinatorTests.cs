using enviro.Models;
using enviro.Services;
using Xunit;

namespace enviro.Tests.Services;

/// <summary>
/// Apply runs through the real <see cref="ApplyCoordinator"/> with a fake adapter that mimics
/// EnvAdapter's order (per scope: Deleted, Modified, Added; user scope first) and cleans each
/// written row through <see cref="IEnvScope.MarkApplied"/>, exactly like the real one.
/// </summary>
public class ApplyCoordinatorTests
{
    private sealed class FakeAdapter(IEnvService env, EnvModel? failOn = null) : IEnvAdapter
    {
        public int Calls;

        public Task<ApplyResult> ApplyAsync()
        {
            Calls++;

            var user = ApplyScope(env.User, EnvironmentalVariableType.User, out var failed, out var error);

            if (failed is not null)
                return Task.FromResult(new ApplyResult(user, 0, EnvironmentalVariableType.User, failed, error));

            var machine = ApplyScope(env.Machine, EnvironmentalVariableType.Machine, out failed, out error);

            if (failed is not null)
                return Task.FromResult(new ApplyResult(user, machine, EnvironmentalVariableType.Machine, failed, error));

            return Task.FromResult(new ApplyResult(user, machine, null, null, null));
        }

        private int ApplyScope(IEnvScope scope, EnvironmentalVariableType type, out EnvModel? failed, out string? error)
        {
            var applied = 0;
            failed = null;
            error = null;

            EnvironmentalVariableState[] order =
            [
                EnvironmentalVariableState.Deleted,
                EnvironmentalVariableState.Modified,
                EnvironmentalVariableState.Added,
            ];

            foreach (var state in order)
            {
                foreach (var model in scope.Rows.OfType<EnvModel>().Where(m => m.State == state).ToList())
                {
                    if (model == failOn)
                    {
                        failed = model;
                        error = "simulated write failure";
                        return applied;
                    }

                    scope.MarkApplied(model, state);
                    applied++;
                }
            }

            return applied;
        }
    }

    private static (EnvService Env, UndoHistory History, FakeAdapter Adapter, ApplyCoordinator Coord) Setup(EnvModel? failOn = null)
    {
        var history = new UndoHistory();
        var env = new EnvService(history);
        var adapter = new FakeAdapter(env, failOn);
        return (env, history, adapter, new ApplyCoordinator(env, adapter, history));
    }

    private static void Seed(EnvService env, string name, string value) =>
        ((EnvScope)env.User).Replace([.. ((EnvScope)env.User).Rows.Cast<EnvModel>(), Workspace.Row(name, value)]);

    [Fact]
    public void PendingChanges_counts_every_non_unchanged_row_across_both_scopes()
    {
        var (env, _, _, coord) = Setup();
        Assert.Equal(0, coord.PendingChanges());

        Seed(env, "A", "v");
        env.User.Update(env.User.GetByName("A")!, "v2"); // Modified
        env.User.Upsert("NEW", "1", overwrite: true);     // Added

        Assert.Equal(2, coord.PendingChanges());
    }

    [Fact]
    public async Task Apply_with_nothing_pending_reports_zero_and_still_calls_the_adapter()
    {
        var (_, _, adapter, coord) = Setup();

        var outcome = await coord.ApplyAsync();

        Assert.True(outcome.Succeeded);
        Assert.Equal("Applied 0 changes", outcome.Message);
        Assert.Equal(1, adapter.Calls);
    }

    [Fact]
    public async Task A_full_apply_run_cleans_every_scope_and_is_one_undo_step()
    {
        var (env, history, _, coord) = Setup();
        ((EnvScope)env.User).Replace([Workspace.Row("A", "v"), Workspace.Row("DEL", "x")]);
        ((EnvScope)env.Machine).Replace([Workspace.Row("M", "v")]);
        var a = env.User.GetByName("A")!;
        var del = env.User.GetByName("DEL")!;
        var m = env.Machine.GetByName("M")!;

        env.User.Update(a, "v2");          // Modified
        env.User.Delete(del);              // Deleted
        env.Machine.Delete(m);             // Deleted
        env.User.Upsert("NEW", "1", true); // Added

        Assert.Equal(4, coord.PendingChanges());

        var outcome = await coord.ApplyAsync();

        Assert.True(outcome.Succeeded);
        Assert.Equal("Applied 4 changes", outcome.Message);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State);
        Assert.Equal("v2", a.OriginalPath);
        Assert.Null(env.User.GetByName("DEL"));   // written deletion left the list
        Assert.Null(env.Machine.GetByName("M"));
        Assert.Equal(EnvironmentalVariableState.Unchanged, env.User.GetByName("NEW")!.State);
        Assert.Equal(0, coord.PendingChanges());
        Assert.True(history.CanUndo);

        env.Undo(); // the whole apply run is a single step

        Assert.NotNull(env.User.GetByName("DEL"));
        Assert.Equal(EnvironmentalVariableState.Deleted, env.User.GetByName("DEL")!.State);
        Assert.Equal(EnvironmentalVariableState.Deleted, env.Machine.GetByName("M")!.State);
        Assert.True(history.CanUndo); // the four original edit steps are still underneath
    }

    [Fact]
    public async Task Apply_of_a_single_change_uses_the_singular_form()
    {
        var (env, _, _, coord) = Setup();
        env.User.Upsert("ENVIRO_TEST_ONE", "1", overwrite: true);

        var outcome = await coord.ApplyAsync();

        Assert.Equal("Applied 1 change", outcome.Message);
    }

    [Fact]
    public async Task A_failed_apply_reports_the_row_the_error_and_what_is_still_pending()
    {
        var (env, history, _, _) = Setup();
        ((EnvScope)env.User).Replace([Workspace.Row("A", "v")]);
        ((EnvScope)env.Machine).Replace([Workspace.Row("M", "v")]);
        var a = env.User.GetByName("A")!;
        var m = env.Machine.GetByName("M")!;
        env.User.Update(a, "v2");
        env.Machine.Delete(m);

        var adapter = new FakeAdapter(env, failOn: m);
        var coord = new ApplyCoordinator(env, adapter, history); // same history the service records on

        var outcome = await coord.ApplyAsync();

        Assert.False(outcome.Succeeded);
        Assert.Contains("Failed to apply changes.", outcome.Message);
        Assert.Contains("Machine variable \"M\" could not be written: simulated write failure", outcome.Message);
        Assert.Contains("1 change was written before the failure.", outcome.Message); // the user row went first
        Assert.Contains("1 change is still pending (1 Machine).", outcome.Message);
        Assert.Equal(EnvironmentalVariableState.Unchanged, a.State); // written and cleaned
        Assert.Equal(EnvironmentalVariableState.Deleted, m.State);   // stopped, still pending
        Assert.Equal(1, coord.PendingChanges());
    }
}
