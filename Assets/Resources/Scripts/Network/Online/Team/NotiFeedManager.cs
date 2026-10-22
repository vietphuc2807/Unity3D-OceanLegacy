using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class NotiFeedManager : MonoBehaviour
{
    public static NotiFeedManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform _container;
    [SerializeField] private NotiFeedItem _itemPrefab;

    [Header("Layout")]
    [Tooltip("Khoảng cách giữa các bảng.")]
    [SerializeField] private float _spacing = 10f;

    [Tooltip("Số bảng tối đa.")]
    [SerializeField] private int _maxVisibleItems = 5;

    [Tooltip("Chiều cao cố định của mỗi notification.")]
    [SerializeField] private float _itemHeight = 64f;

    [Header("Animation")]
    [SerializeField] private float _repositionDuration = 0.32f;

    [SerializeField] private Ease _repositionEase =
        Ease.OutCubic;

    [Header("Display")]
    [SerializeField] private float _displayDuration = 4f;

    [Header("Extra Polish")]
    [Tooltip("Delay rất nhỏ giữa các bảng khi xuất hiện.")]
    [SerializeField] private float _spawnStagger = 0.025f;

    private readonly List<NotiFeedItem> _activeItems =
        new List<NotiFeedItem>();

    private readonly Dictionary<
        NotiFeedItem,
        Coroutine
    > _lifetimeRoutines =
        new Dictionary<
            NotiFeedItem,
            Coroutine
        >();


    // ============================================================
    // UNITY
    // ============================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Đảm bảo container không bị scale lạ.
        if (_container != null)
        {
            _container.localScale = Vector3.one;
        }
    }


    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }


    // ============================================================
    // SHOW
    // ============================================================

    public void ShowNotification(
        string playerName,
        int amount)
    {
        if (_container == null)
        {
            Debug.LogWarning(
                "[NotiFeedManager] Container chưa được gán."
            );

            return;
        }

        if (_itemPrefab == null)
        {
            Debug.LogWarning(
                "[NotiFeedManager] Item Prefab chưa được gán."
            );

            return;
        }

        if (amount == 0)
            return;


        // --------------------------------------------------------
        // CREATE
        // --------------------------------------------------------

        NotiFeedItem item =
            Instantiate(
                _itemPrefab,
                _container
            );


        RectTransform rect =
            item.RectTransform;


        if (rect != null)
        {
            // QUAN TRỌNG:
            // Reset toàn bộ transform để tránh prefab
            // hoặc object cũ giữ scale/position.
            rect.localScale =
                Vector3.one;

            rect.localRotation =
                Quaternion.identity;

            rect.anchoredPosition =
                Vector2.zero;

            rect.localPosition =
                new Vector3(
                    rect.localPosition.x,
                    rect.localPosition.y,
                    0f
                );
        }


        // --------------------------------------------------------
        // INSERT FIRST
        // --------------------------------------------------------

        _activeItems.Insert(
            0,
            item
        );


        // --------------------------------------------------------
        // LIMIT ITEMS
        // --------------------------------------------------------

        while (_activeItems.Count >
               Mathf.Max(
                   1,
                   _maxVisibleItems
               ))
        {
            NotiFeedItem oldest =
                _activeItems[
                    _activeItems.Count - 1
                ];

            RemoveItem(oldest);
        }


        // --------------------------------------------------------
        // POSITION NEW ITEM
        // --------------------------------------------------------

        if (rect != null)
        {
            rect.anchoredPosition =
                new Vector2(
                    0f,
                    0f
                );
        }


        item.Setup(
            playerName,
            amount
        );


        // --------------------------------------------------------
        // REPOSITION STACK
        // --------------------------------------------------------

        RepositionAll();


        // --------------------------------------------------------
        // AUTO REMOVE
        // --------------------------------------------------------

        Coroutine routine =
            StartCoroutine(
                AutoRemoveAfterDelay(
                    item,
                    _displayDuration
                )
            );

        _lifetimeRoutines[item] =
            routine;
    }


    // ============================================================
    // AUTO REMOVE
    // ============================================================

    private IEnumerator AutoRemoveAfterDelay(
        NotiFeedItem item,
        float delay)
    {
        yield return new WaitForSeconds(
            delay
        );

        RemoveItem(item);
    }


    // ============================================================
    // REMOVE
    // ============================================================

    private void RemoveItem(
        NotiFeedItem item)
    {
        if (item == null)
            return;

        if (!_activeItems.Contains(item))
            return;


        // --------------------------------------------------------
        // STOP TIMER
        // --------------------------------------------------------

        if (_lifetimeRoutines.TryGetValue(
            item,
            out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            _lifetimeRoutines.Remove(item);
        }


        // --------------------------------------------------------
        // REMOVE FROM LIST FIRST
        // --------------------------------------------------------

        _activeItems.Remove(item);


        // --------------------------------------------------------
        // EXIT ANIMATION
        // --------------------------------------------------------

        item.FadeOutAndDisable(
            _repositionDuration,
            () =>
            {
                if (item != null)
                {
                    Destroy(
                        item.gameObject
                    );
                }
            }
        );


        // --------------------------------------------------------
        // MOVE REMAINING ITEMS
        // --------------------------------------------------------

        RepositionAll();
    }


    // ============================================================
    // REPOSITION
    // ============================================================

    private void RepositionAll()
    {
        float step =
            _itemHeight +
            _spacing;


        for (int i = 0;
             i < _activeItems.Count;
             i++)
        {
            NotiFeedItem item =
                _activeItems[i];

            if (item == null)
                continue;


            float targetY =
                -i * step;


            item.AnimateToPosition(
                targetY,
                _repositionDuration,
                _repositionEase
            );
        }
    }


    // ============================================================
    // CLEAR ALL
    // ============================================================

    public void ClearAll()
    {
        StopAllCoroutines();

        _lifetimeRoutines.Clear();

        for (int i = 0;
             i < _activeItems.Count;
             i++)
        {
            if (_activeItems[i] != null)
            {
                Destroy(
                    _activeItems[i].gameObject
                );
            }
        }

        _activeItems.Clear();
    }
}