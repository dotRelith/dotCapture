using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dotCapture
{
    internal class SupportedLanguagesDictionary
    {
        public static Dictionary<string, string> dictionary = new Dictionary<string, string>()
        {
            { "English", "en-US" },
            { "Español", "es-ES" },
            { "Italiano", "it-IT" },
            { "Portugues (Brasil)", "pt-BR" }
        };
    }
}
