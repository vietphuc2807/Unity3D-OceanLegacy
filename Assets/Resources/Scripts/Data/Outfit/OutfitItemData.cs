using UnityEngine;

[CreateAssetMenu(fileName = "OutfitItem", menuName = "Data/Outfit Item")]
public class OutfitItemData : ScriptableObject
{
    [SerializeField] private string _itemId;
    [SerializeField] private Sprite _icon;
    [SerializeField] private GameObject _prefab; 

    [Header("Fit Offset")]
    [SerializeField] private Vector3 _localPosition;
    [SerializeField] private Vector3 _localRotationEuler;
    [SerializeField] private Vector3 _localScale = Vector3.one;

    public string ItemId => _itemId;
    public Sprite Icon => _icon;
    public GameObject Prefab => _prefab;
    public Vector3 LocalPosition => _localPosition;
    public Vector3 LocalRotationEuler => _localRotationEuler;
    public Vector3 LocalScale => _localScale;
}