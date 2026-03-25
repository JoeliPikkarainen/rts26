using System;
using UnityEngine;

public static class GameEvents
{
    public static Action<HitEvent> OnHit;
    public static Action<PickupEvent> OnPickup;
}

public struct HitEvent
{
    public GameObject src;
    public GameObject dst;

    public struct HitCtx
    {
        public int dmg;
    };

    public HitCtx ctx;
    public HitEvent(GameObject src, GameObject dst, HitCtx ctx)
    {
        this.src = src;
        this.dst = dst;
        this.ctx = ctx;
    }
}

public struct PickupEvent
{
    public GameObject src;
    public GameObject itemObject;
    public ItemData itemData;

    public PickupEvent(GameObject src, GameObject itemObject, ItemData itemData)
    {
        this.src = src;
        this.itemObject = itemObject;
        this.itemData = itemData;
    }
}