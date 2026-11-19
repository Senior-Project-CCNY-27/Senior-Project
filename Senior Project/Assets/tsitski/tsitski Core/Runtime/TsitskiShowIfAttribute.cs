using System;

namespace Tsitski.Core {
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TsitskiShowIfAttribute : Attribute {
        public readonly string condition;

        public TsitskiShowIfAttribute(string condition) => this.condition = condition;
    }
}
