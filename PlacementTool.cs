using System;
using Core;
using GamePlay.States;
using Inventory;
using Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BuildSystem
{
    public class PlacementTool : SingletonGObj<PlacementTool>
    {
        public event Action<BuildItemSO, GameObject> OnConfirmBuild;

        [SerializeField] private float maxDistance = 10f;
        [SerializeField] private InputActionReference confirmButton;
        [SerializeField] private InputActionReference rotateButton;
        [SerializeField] private FloorOrWallDetector floorOrWallDetector;

        private BuildItemSO _currentBuildItem;
        private State _currentState;
        private GameObject _currentGameObject;
        private PlaceableUnit _currentPlaceableUnit;
        private bool _canBuild;
        private Camera _camera;

        private void Start()
        {
            _currentState = State.EndBuilding;
            _camera = Camera.main;
            InventoryManager.Instance.onActiveSlotChanged += OnActiveSlotChanged;
        }

        private void OnDestroy()
        {
            InventoryManager.Instance.onActiveSlotChanged -= OnActiveSlotChanged;
        }

        private void OnEnable()
        {
            confirmButton.action.started += ConfirmBuild;
            rotateButton.action.started += Rotate;
        }

        private void OnDisable()
        {
            confirmButton.action.started -= ConfirmBuild;
            rotateButton.action.started -= Rotate;
        }

        private void OnActiveSlotChanged()
        {
            InventorySlot slot = InventoryManager.Instance.GetActiveSlot();

            if (slot?.item == null || slot.item.count <= 0 || slot.item.itemSO is not BuildItemSO buildItemSo)
            {
                StopBuild();
                return;
            }

            if (_currentState == State.Building && _currentBuildItem == buildItemSo) return;

            StopBuild();
            StartBuild(buildItemSo);
        }

        private void StartBuild(BuildItemSO buildBlock)
        {
            if (GameStateManager.Instance.GetCurrentState() is not GamePlayState) return;
            if (buildBlock == null) return;

            _currentBuildItem = buildBlock;
            _currentGameObject = Instantiate(buildBlock.prefab);
            _currentState = State.Building;

            GameStateManager.Instance.GoToBuild();

            _currentPlaceableUnit = _currentGameObject.GetComponent<PlaceableUnit>();
            _currentPlaceableUnit?.StartBuild();
        }

        private void StopBuild()
        {
            if (_currentState != State.Building) return;

            if (_currentGameObject != null)
                Destroy(_currentGameObject);

            _currentGameObject = null;
            _currentState = State.EndBuilding;

            GameStateManager.Instance.GoToGamePlay();
            CrosshairManager.Instance.ResetColor();
        }

        private void ConfirmBuild(InputAction.CallbackContext callbackContext)
        {
            if (!_canBuild || GameStateManager.Instance.GetCurrentState() is not BuildState) return;
            if (_currentBuildItem == null || _currentGameObject == null) return;

            OnConfirmBuild?.Invoke(_currentBuildItem, _currentGameObject);

            _currentPlaceableUnit?.StopBuild();

            _currentGameObject = null;
            _currentState = State.EndBuilding;

            InventoryManager.Instance.RemoveItemAtActiveSlot(1);
            CrosshairManager.Instance.ResetColor();
            GameStateManager.Instance.GoToGamePlay();

            OnActiveSlotChanged();
        }

        private void Update()
        {
            if (_currentState != State.Building || _currentGameObject == null) return;

            Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                PlacementType targetPlacementType = floorOrWallDetector.GetPlacementType(hit.normal);
                _canBuild = CanBuildHere(targetPlacementType);

                _currentGameObject.transform.position = hit.point;
                _currentPlaceableUnit?.Rotate(hit.normal);
            }
            else
            {
                _currentGameObject.transform.position = ray.GetPoint(maxDistance);
                _canBuild = false;
            }

            CrosshairManager.Instance.EditColor(_canBuild ? Color.green : Color.red);
        }

        public void Rotate(InputAction.CallbackContext callbackContext)
        {
            if (_currentState != State.Building) return;
            _currentPlaceableUnit?.RotateByKey();
        }

        private bool CanBuildHere(PlacementType targetType) =>
            _currentBuildItem.placementType == PlacementType.Free || targetType == _currentBuildItem.placementType;
    }

    public enum State { Building, EndBuilding }
}