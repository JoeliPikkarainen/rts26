using UnityEngine;

public interface ITextInfoOverlay
{
    /// <summary>
    /// Returns debug/info text to display when hovering over this object
    /// </summary>
    string GetInfoText();
}
