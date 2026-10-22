using System.Collections;
using Fusion;
using UnityEngine;

public class HotbarUI : MonoBehaviour
{
    [Header("Hotbar Slots")]
    [SerializeField]
    private HotbarSlotUI[] _slotUIs = new HotbarSlotUI[7];

    [Header("Item Name")]
    [SerializeField]
    private HotbarItemNameUI _itemNameUI;

    private PlayerInventory _inventory;


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        StartCoroutine(FindLocalInventory());
    }


    // ============================================================
    // FIND LOCAL INVENTORY
    // ============================================================

    private IEnumerator FindLocalInventory()
    {
        while (_inventory == null)
        {
            PlayerInventory[] allInventories =
                FindObjectsByType<PlayerInventory>(
                    FindObjectsSortMode.None
                );

            foreach (PlayerInventory inv in allInventories)
            {
                if (inv == null)
                    continue;

                NetworkObject netObj =
                    inv.GetComponent<NetworkObject>();

                if (netObj != null &&
                    netObj.HasInputAuthority)
                {
                    _inventory = inv;
                    break;
                }
            }

            yield return null;
        }


        // ========================================================
        // TRUYỀN INVENTORY CHO HOTBAR ITEM NAME UI
        // ========================================================

        if (_itemNameUI != null)
        {
            _itemNameUI.SetInventory(_inventory);
        }


        // ========================================================
        // SUBSCRIBE EVENT
        // ========================================================

        _inventory.OnSelectedSlotChanged +=
            HandleSelectedSlotChanged;


        // ========================================================
        // REFRESH
        // ========================================================

        RefreshAllIcons();

        HandleSelectedSlotChanged(
            _inventory.SelectedIndex,
            _inventory.GetSelectedItem()
        );
    }


    // ============================================================
    // DESTROY
    // ============================================================

    private void OnDestroy()
    {
        if (_inventory != null)
        {
            _inventory.OnSelectedSlotChanged -=
                HandleSelectedSlotChanged;
        }
    }


    // ============================================================
    // REFRESH ALL ICONS
    // ============================================================

    private void RefreshAllIcons()
    {
        if (_inventory == null)
            return;

        ItemData[] slots =
            _inventory.Slots;

        for (int i = 0;
             i < _slotUIs.Length;
             i++)
        {
            if (_slotUIs[i] == null)
                continue;

            ItemData item =
                i < slots.Length
                    ? slots[i]
                    : null;

            _slotUIs[i].SetItem(item);
        }
    }


    // ============================================================
    // SELECTED SLOT CHANGED
    // ============================================================

    private void HandleSelectedSlotChanged(
        int index,
        ItemData item)
    {
        // --------------------------------------------------------
        // Refresh icon
        // --------------------------------------------------------

        RefreshAllIcons();


        // --------------------------------------------------------
        // Selected border
        // --------------------------------------------------------

        for (int i = 0;
             i < _slotUIs.Length;
             i++)
        {
            if (_slotUIs[i] == null)
                continue;

            _slotUIs[i].SetSelected(
                i == index
            );
        }


        // ========================================================
        // QUAN TRỌNG:
        // KHÔNG GỌI ShowItemName() Ở ĐÂY NỮA
        // ========================================================
        //
        // HotbarItemNameUI đã tự subscribe vào
        // PlayerInventory.OnSelectedSlotChanged.
        //
        // Nó sẽ tự xử lý:
        //
        // Cá:
        // Bluegill - 1.25 KG
        //
        // Item thường:
        // Fishing Rod
        //
        // ========================================================
    }
}