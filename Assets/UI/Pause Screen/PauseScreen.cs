using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PauseScreen : MonoBehaviour
{

    [SerializeField]
    private SpriteRenderer _target;

    [SerializeField]
    private CanvasGroup _canvas;

    public bool Toggle()
    {
        if (_canvas.alpha > 0.5)
        {
            Unpause();
            return false;
        }
        else
        {
            Pause();
            return true;
        }
    }

    public void Pause()
    {
        StartCoroutine(PauseRoutine());
    }

    private IEnumerator PauseRoutine()
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        Rect rec = new Rect(0, 0, tex.width, tex.height);
        var created = Sprite.Create(tex, rec, new Vector2(0, 0), 1);
        _target.sprite = created;
        yield return new WaitForEndOfFrame();
        _canvas.alpha = 1;
    }

    public void Unpause()
    {
        if (_target.sprite)
        {
            Destroy(_target.sprite.texture);
            Destroy(_target.sprite);
        }
        _canvas.alpha = 0;
    }

}
