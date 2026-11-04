using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DraggableElement : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField]
    private RectTransform _container;

    [SerializeField]
    private Vector2 _padding = new Vector2(16f, 16f);

    private bool _isClicked = false;
    private Vector3 _clickOffset = Vector2.zero;

    [SerializeField]
    private Vector2 _localStartPosition;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isClicked = true;
        _clickOffset = this.transform.position - Input.mousePosition;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isClicked = false;
        _clickOffset = Vector2.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        this.transform.localPosition = _localStartPosition;
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (_isClicked)
        {
            this.transform.position = (Input.mousePosition + _clickOffset);
        }
        var rectTransform = GetComponent<RectTransform>();
        // Access the width and height
        var width = rectTransform.rect.width;
        var height = rectTransform.rect.height;
        // Access the width and height
        var parentWidth = _container.rect.width;
        var parentHeight = _container.rect.height;
        this.transform.localPosition = new Vector3(
            Mathf.Clamp(this.transform.localPosition.x, (-parentWidth / 2f + width / 2f + _padding.x), (parentWidth / 2f - width / 2f - _padding.x)),
            Mathf.Clamp(this.transform.localPosition.y, (-parentHeight / 2f + height / 2f + _padding.y), (parentHeight / 2f - height / 2f - _padding.y)),
            0);

    }
}
