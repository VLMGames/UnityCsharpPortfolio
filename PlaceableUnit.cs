using System;
using Electricity;
using SaveSystem;
using Units;
using UnityEngine;

namespace BuildSystem
{
    [RequireComponent(typeof(Collider))]
    public class PlaceableUnit : MonoBehaviour
    {
        private ElectricUnit _electricUnit;
        private Collider[] _colliders;
        private ISavedUnit _savedUnit;

        public bool needRotateToSolid;
        private float _userRotation;

        public event Action OnStartBuild;
        public event Action OnStopBuild;

        private void Awake()
        {
            _electricUnit = GetComponent<ElectricUnit>();
            _colliders = GetComponentsInChildren<Collider>();
            _savedUnit = GetComponent<ISavedUnit>();
        }

        private void OnDestroy()
        {
            SaveUtility.Unregister(_savedUnit);
        }

        public void Rotate(Vector3 surfaceNormal)
        {
            if (!needRotateToSolid) return;

            Quaternion baseRotation = Quaternion.LookRotation(surfaceNormal);
            Quaternion userOffset = Quaternion.AngleAxis(_userRotation, Vector3.forward);
            transform.rotation = baseRotation * userOffset;
        }

        public void RotateByKey()
        {
            if (!needRotateToSolid)
            {
                transform.Rotate(Vector3.up, 90f, Space.World);
            }
        }

        public void StartBuild()
        {
            OnStartBuild?.Invoke();

            if (_electricUnit != null) _electricUnit.enabled = false;
            foreach (Collider col in _colliders) col.enabled = false;
        }

        public void StopBuild()
        {
            OnStopBuild?.Invoke();

            if (_savedUnit != null) SaveUtility.Register(_savedUnit);

            if (_electricUnit != null) _electricUnit.enabled = true;
            foreach (Collider col in _colliders) col.enabled = true;
        }
    }
}