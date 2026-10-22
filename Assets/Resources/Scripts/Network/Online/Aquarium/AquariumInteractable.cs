using UnityEngine;

public class AquariumInteractableObject : InteractableObject
{
    [Header("Aquarium View")]
    [SerializeField] private AquariumView _aquariumView;

    public override void Interact()
    {
        if (Active != null)
            return;

        if (_aquariumView == null)
        {
            Debug.LogWarning($"[AquariumInteractableObject] ({name}) _aquariumView is NULL.");
            return;
        }

        FreezePlayer();

        _aquariumView.Open(UnfreezePlayer);
    }

    public override void Close()
    {
        if (_aquariumView == null)
        {
            UnfreezePlayer();
            return;
        }

        _aquariumView.Close();
    }
}