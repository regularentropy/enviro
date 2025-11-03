using System.ComponentModel;

namespace enviro.Models
{
    internal class EnvModelBundle
    {
        public BindingList<EnvModel> User { get; set; } = [];
        public BindingList<EnvModel> Machine { get; set; } = [];
    }
}
