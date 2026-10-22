using UnityEngine;

/// <summary>
/// Raycast tu giua man hinh, tim InteractableObject gan nhat de hien
/// prompt (_interactUI) + bam E goi Interact().
///
/// SUA LOI: truoc day luc PlayerCursor.IsUIOpen == true (dang mo 1
/// view/panel khac, VD AquariumView, dialog NPC...), Update() return
/// ngay lap tuc MA KHONG goi CheckInteractable() - trong khi
/// CheckInteractable() la ham DUY NHAT bat/tat _interactUI. Neu luc
/// bam E de mo view, prompt dang active(true), no se bi "ket" hien
/// mai tren man hinh cho toi khi IsUIOpen ve false, du camera co the
/// dang bay di cho khac (VD AquariumView) khong con nhin vao
/// interactable do nua.
///
/// Sua: khi IsUIOpen == true, CHU DONG tat _interactUI di + reset
/// _currentInteractable, thay vi chi return suong.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private GameObject _interactUI;

    [Header("Settings")]
    [SerializeField] private float _interactDistance = 3f;
    [SerializeField] private LayerMask _interactableLayer;

    private InteractableObject _currentInteractable;

    private void Update()
    {
        if (PlayerCursor.IsUIOpen)
        {
            HideInteractPrompt();
            return;
        }

        CheckInteractable();

        if (_currentInteractable != null &&
            Input.GetKeyDown(KeyCode.E))
        {
            _currentInteractable.Interact();
        }
    }

    private void CheckInteractable()
    {
        Ray ray = _playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            _interactDistance,
            _interactableLayer))
        {
            InteractableObject interactable =
                hit.collider.GetComponentInParent<InteractableObject>();

            if (interactable != null)
            {
                _currentInteractable = interactable;
                _interactUI.SetActive(true);
                return;
            }
        }

        _currentInteractable = null;

        if (_interactUI != null)
            _interactUI.SetActive(false);
    }

    /// <summary>
    /// An prompt + quen di interactable dang nham toi - goi khi
    /// PlayerCursor.IsUIOpen (dang trong 1 view/panel khac), de
    /// _interactUI khong bi ket lai o trang thai truoc do.
    /// </summary>
    private void HideInteractPrompt()
    {
        _currentInteractable = null;

        if (_interactUI != null)
            _interactUI.SetActive(false);
    }
}