using enviro.Models;
using enviro.Static;

namespace enviro.Factories;

/// <summary>
/// Factory interface for creating TabPage controls.
/// </summary>
public interface ITabFactory
{
    /// <summary>
    /// Creates a TabPage for the specified environmental variable type.
    /// </summary>
    /// <param name="tab">The type of environmental variable (User or Machine).</param>
    /// <param name="elevate">
    /// Optional action that starts an elevated instance. When the Machine scope is read-only and
    /// this is supplied, the page shows a banner explaining the restriction.
    /// </param>
    /// <returns>A configured TabPage containing a filter box, an optional read-only banner and a DataGridView.</returns>
    TabPage Create(EnvironmentalVariableType tab, Action? elevate = null);
}

/// <summary>
/// Factory for creating TabPage controls with DataGridView for environmental variables.
/// </summary>
/// <param name="gridFactory">The factory for creating DataGridView instances.</param>
internal sealed class TabFactory(IEnvGridFactory gridFactory) : ITabFactory
{
    /// <summary>
    /// Creates a TabPage with a DataGridView for displaying environmental variables.
    /// </summary>
    /// <param name="currentTab">The type of environmental variable (User or Machine).</param>
    /// <param name="elevate">
    /// Optional action that starts an elevated instance; used by the read-only Machine banner.
    /// </param>
    /// <returns>A TabPage tagged with the variable type and containing a grid.</returns>
    public TabPage Create(EnvironmentalVariableType currentTab, Action? elevate = null)
    {
        // The tab carries its scope in Tag; the tab text is presentation only.
        TabPage tab = new(currentTab.ToString()) { Tag = currentTab };

        DataGridView grid = gridFactory.Create(currentTab);

        TextBox filter = new()
        {
            Dock = DockStyle.Top,
            PlaceholderText = "Filter by name or value (Ctrl+F)",
            BorderStyle = BorderStyle.FixedSingle
        };

        // Fill first, then the top-docked controls: WinForms docks controls in reverse add order.
        tab.Controls.Add(grid);
        tab.Controls.Add(filter);

        if (currentTab == EnvironmentalVariableType.Machine && !AdminChecker.IsAdmin() && elevate is not null)
            tab.Controls.Add(CreateReadOnlyBanner(elevate));

        gridFactory.AttachFilter(grid, filter);

        return tab;
    }

    /// <summary>
    /// Builds the slim banner that explains why the Machine scope is read-only and offers elevation.
    /// </summary>
    /// <param name="elevate">The action that starts an elevated instance.</param>
    /// <returns>A top-docked panel containing the explanation and an <b>Elevate</b> button.</returns>
    private static Panel CreateReadOnlyBanner(Action elevate)
    {
        var banner = new Panel
        {
            Dock = DockStyle.Top,
            BackColor = ThemePalette.InfoBackground,
            Padding = new Padding(8, 4, 8, 4),
        };

        banner.Height = banner.Font.Height + 16;

        var label = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Read-only — run as administrator to edit machine variables",
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
        };

        var button = new Button
        {
            Dock = DockStyle.Right,
            Text = "Elevate",
            Width = 90,
            UseVisualStyleBackColor = true,
        };

        button.Click += (_, _) => elevate();

        banner.Controls.Add(label);
        banner.Controls.Add(button);

        return banner;
    }
}
