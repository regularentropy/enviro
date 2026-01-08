using System.ComponentModel;

namespace enviro.Models
{
    /// <summary>
    /// Contains collections of environmental variables for User and Machine scopes.
    /// </summary>
    internal class EnvModelBundle
    {
        public BindingList<EnvModel> User { get; set; } = [];
       
        public BindingList<EnvModel> Machine { get; set; } = [];
    }
}
