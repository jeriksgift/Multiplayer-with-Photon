using UnityEngine;

public sealed class SoldierRifleIK : MonoBehaviour
{
    public PlayerWithGFXPresentation presentation;
    Animator animator;
    void Awake() { animator = GetComponent<Animator>(); }
    void OnAnimatorIK(int layerIndex) { if (!animator) animator = GetComponent<Animator>(); if (presentation) presentation.ApplyHandIK(animator); }
}
