using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using DG.Tweening;


[SelectionBase]
public class QuatumPig : MonoBehaviour
{
    [SerializeField] ElectronsTrigger _electronsTrigger;
    [SerializeField] QuatumPigController _controller;
    [SerializeField] Transform _piggoMesh;
    [SerializeField] Transform _piggoFan;
    
    ElectronOrbit _electronOrbit;

    Electron _preparedForShootElectron;
    float _shootPreparationTime = 0;
    float _fanSpeed = 0;
    PigUI _ui;
    RaycastHit[] _pigtoms = new RaycastHit[1];
    Pigtom pigtomToTransform;


    public void Awake()
    {
        _electronsTrigger.OnElectronEnter += OnElectronActivity;
        _electronsTrigger.OnElectronExit += OnElectronActivity;

        _controller.OnEatElectron += EatElectronClicked;
        _controller.OnCollectElectron += CollectElectronClicked;
        _controller.OnShootElectron += ShootElectron;
        _controller.ScrollWheelUp += () => OnChangeElectron(true);
        _controller.ScrollWheelDown += () => OnChangeElectron(false);

        var electronsObj = new GameObject("PigOrbit");
        electronsObj.transform.parent = transform;
        var orbit = electronsObj.AddComponent<ElectronOrbit>();
        orbit.Setup(transform, 1.5f, 5, 0, 10, ElectronType.Red, electronsObj.transform, 0.1f);
        _electronOrbit = orbit;

        _piggoMesh.DOLocalMoveZ(0.1f, 1).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);

        _electronsTrigger.SetIgnoredOrbit(_electronOrbit);
    }

    void Start()
    {
        _ui = ServiceLocator.Get<PigUI>();
    }


    private void ShootElectron()
    {
        if (_electronOrbit.ElectronsNum == 0 && _preparedForShootElectron == null)
            return;

        if (_preparedForShootElectron == null)
        {
            PrepareForShooting();
        }
        else
        {
            _preparedForShootElectron.TargetPosition = transform.position + transform.forward * 2;
            _preparedForShootElectron.Launch(transform.forward * 0.1f);
            Debug.Log("Electron launched");
            _preparedForShootElectron.transform.parent = null;
            _preparedForShootElectron = null;
        }
    }

    private void OnChangeElectron(bool next)
    {
        if (_preparedForShootElectron == null || _electronOrbit.ElectronsNum == 0)
            return;

        Electron nextElectron;

        if (next)
            nextElectron = _electronOrbit.GetElectron(0);
        else
            nextElectron = _electronOrbit.GetElectron(_electronOrbit.ElectronsNum - 1);
        
        _electronOrbit.RemoveElectron(nextElectron);
        _electronOrbit.AddNewElectron(_preparedForShootElectron);
        _preparedForShootElectron = nextElectron;
    }

    private void FixedUpdate()
    {
        if (_preparedForShootElectron && Time.time - _shootPreparationTime > 3)
        {
            _electronOrbit.AddNewElectron(_preparedForShootElectron);
            _preparedForShootElectron = null;
        }
        else if (_preparedForShootElectron)
        {
            _preparedForShootElectron.TargetPosition = transform.position + transform.forward * 2;
        }


        // no time to catch all possible events


        ManageUI();
        RotateAssFan();
    }

    private void PrepareForShooting()
    {
        _preparedForShootElectron = _electronOrbit.GetElectron(0);
        _preparedForShootElectron.Orbit.RemoveElectron(_preparedForShootElectron);

        _shootPreparationTime = Time.time;
    }

    private void RotateAssFan()
    {
        float targetSpeed = 60 * Time.deltaTime * _controller.TotalForwardSpeed;
        _fanSpeed = Mathf.MoveTowards(_fanSpeed, targetSpeed, 0.2f);
        _piggoFan.Rotate(Vector3.up, _fanSpeed, Space.Self);
    }

    private void ManageUI()
    {
        if (CanCollectElectron())
            _ui.OnElectronInSight();
        else
            _ui.OnElectronOutOfSight();

        var pigtom = Physics.RaycastNonAlloc(transform.position, transform.forward, _pigtoms, 6, Globals.PigtomMask);
        pigtomToTransform = null;

        if (pigtom > 0)
        {
            var hit = _pigtoms[0];

            if (hit.collider != null && hit.collider.TryGetComponent(out Pigtom p) &&
                !p.StartedTransformation)
            {
                _ui.SetMakeOatiumActive(true);
                pigtomToTransform = p;
            }
            else
                _ui.SetMakeOatiumActive(false);
        }
        else
            _ui.SetMakeOatiumActive(false);
    }

    private void OnElectronActivity(Electron electron)
    {
    }

    private void EatElectronClicked()
    {
        if (pigtomToTransform != null)
        {
            pigtomToTransform.StartTransformation();
            pigtomToTransform = null;
            Debug.Log("Pigtom transformed");
        }
        // else if (_electronsTrigger.NearestElectron != null)
        // {
        //     _electronsTrigger.NearestElectron.gameObject.SetActive(false);
        //     Debug.Log("Electron eaten");
        // }
    }

    private void CollectElectronClicked()
    {
        if (CanCollectElectron())
        {
            var electron = _electronsTrigger.NearestElectron;
            if (electron.Orbit != null)
                electron.Orbit.RemoveElectron(electron);
            _electronOrbit.AddNewElectron(electron);
            OnElectronActivity(electron);
            Debug.Log("Electron collected");
        }
    }

    private bool CanCollectElectron()
    {
        var nearestElectron = _electronsTrigger.NearestElectron;

        return _electronsTrigger.NearestElectron != null 
            && _electronOrbit.ElectronsNum < GameSettings.MaxCollectedElectrons
            && _preparedForShootElectron != nearestElectron
                && !nearestElectron.IsLaunched;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 6);
    }
}
