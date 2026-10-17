using JetBrains.Annotations;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace CWF;

[UsedImplicitly]
public class ModuleGraphicData {
    [NoTranslate]
    [UsedImplicitly]
    public string? texturePath;

    [UsedImplicitly]
    public Vector2? offset;

    [UsedImplicitly]
    public float? scale;
}