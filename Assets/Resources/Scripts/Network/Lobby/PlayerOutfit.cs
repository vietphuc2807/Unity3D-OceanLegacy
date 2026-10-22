using Fusion;
using UnityEngine;

public class PlayerOutfit : NetworkBehaviour
{
    [SerializeField] private OutfitCatalog _catalog;
    [SerializeField] private Transform _headBoneOverride; 
    [SerializeField] private Animator _animator;

    [Networked, OnChangedRender(nameof(OnEquippedItemChanged))]
    private NetworkString<_32> EquippedItemId { get; set; }

    private Transform _headBone;
    private GameObject _currentInstance;

    private const string SelfHiddenLayerName = "SelfHiddenGear";

    public override void Spawned()
    {
        ResolveHeadBone();

        if (!string.IsNullOrEmpty(EquippedItemId.ToString()))
        {
            ApplyItem(EquippedItemId.ToString());
        }
    }

    private void ResolveHeadBone()
    {
        if (_headBoneOverride != null)
        {
            _headBone = _headBoneOverride;
            return;
        }

        if (_animator != null && _animator.isHuman)
        {
            _headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
        }
    }

    public void RequestEquip(string itemId)
    {
        if (!Object.HasInputAuthority)
            return;

        RPC_SetEquippedItem(itemId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetEquippedItem(string itemId)
    {
        EquippedItemId = itemId;
        FusionRoomManager.Instance?.SetPlayerOutfit(Object.InputAuthority, itemId);
    }

    private void OnEquippedItemChanged()
    {
        ApplyItem(EquippedItemId.ToString());
    }

    private void ApplyItem(string itemId)
    {
        if (_currentInstance != null)
        {
            Destroy(_currentInstance);
            _currentInstance = null;
        }

        if (string.IsNullOrEmpty(itemId) || itemId == "none" || _catalog == null || _headBone == null)
            return;

        var data = _catalog.GetById(itemId);

        if (data == null || data.Prefab == null)
            return;

        _currentInstance = Instantiate(data.Prefab, _headBone);

        _currentInstance.transform.localPosition = data.LocalPosition;
        _currentInstance.transform.localRotation = Quaternion.Euler(data.LocalRotationEuler);
        _currentInstance.transform.localScale = data.LocalScale;

        // Chỉ với chính chủ (local player), đưa mũ vào layer riêng để camera của họ tự loại trừ
        if (Object.HasInputAuthority)
        {
            SetLayerRecursively(_currentInstance, LayerMask.NameToLayer(SelfHiddenLayerName));
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (layer < 0)
            return; // layer chưa được tạo trong Project Settings

        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void ApplyStoredOutfit(string itemId)
    {
        if (!Object.HasStateAuthority)
            return;

        EquippedItemId = itemId;
    }
}