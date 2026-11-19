using System.Collections.Generic;
using UnityEngine;
using Tsitski.Core;

namespace Tsitski.ProBuilderTools {
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public class TsitskiProBuilderBakedCuts : MonoBehaviour {
        [Tooltip(Tooltips.BakedCutters)] public List<TsitskiProBuilderCutter> cutters = new();
    }
}
