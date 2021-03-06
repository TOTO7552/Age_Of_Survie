﻿using UnityEngine;

public class FogOfWarUnit : MonoBehaviour
{
    public float radius = 5.0f;
    [Range(0.0f, 180.0f)]
    public float angle = 180;

    public float updateFrequency { get { return FogOfWar.current.updateFrequency; } }
    float _nextUpdate = 0.0f;

    public LayerMask lineOfSightMask = 0;

    Transform _transform;

    void Start()
    {
        _transform = transform;
        _nextUpdate = Random.Range(0.0f, updateFrequency);
    }

    void Update()
    {
        _nextUpdate -= Time.deltaTime;
        if (_nextUpdate > 0)
            return;

        _nextUpdate = updateFrequency;
        FogOfWar.current.Unfog(_transform.position, radius, angle, _transform.forward + _transform.up, lineOfSightMask);
    }
}
