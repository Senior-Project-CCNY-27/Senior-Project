using System.Collections.Generic;
using UnityEngine;

namespace Tsitski.Core {
    public interface IPathOwner {
        List<Vector3> localPath { get; set; }
        bool pathInStats { get; }
        void Rebuild();
    }

    // A generator whose height its match-height pillars follow, so dragging one can change it.
    public interface IHeightOwner {
        Object heightHolder { get; } // where the height is stored: the generator or its stats
        float ownerHeight { get; set; }
        void ApplyHeight();
    }
}
