using System;

namespace Tsitski.Core {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class TsitskiMenuAttribute : Attribute {
        public readonly string path;
        public string validate;
        public string shortcut;
        public int priority = 1000;

        public TsitskiMenuAttribute(string path) => this.path = path;
    }
}
