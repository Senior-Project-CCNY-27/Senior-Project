using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tsitski.Core {

    // Drawer behind [FancySO]: the popup's base type comes from the field's declared
    // type (unwrapping arrays/lists for collection elements) instead of a generic parameter.
    [CustomPropertyDrawer(typeof(FancySOAttribute))]
    public class FancySOAttributeDrawer : FancySODrawerBase {

        protected override bool CreateAsSubAsset => ((FancySOAttribute)attribute).createAsSubAsset;

        protected override Type BaseType {
            get {
                Type type = fieldInfo.FieldType;

                if (type.IsArray)
                    return type.GetElementType();

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    return type.GetGenericArguments()[0];

                return type;
            }
        }
    }
}
