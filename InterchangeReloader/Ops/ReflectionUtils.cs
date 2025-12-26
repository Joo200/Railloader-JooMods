using System.Collections.Generic;
using System.Reflection;
using Model;
using Model.Ops;
using Model.Definition.Data;
using Model.Ops.Definition;
using UnityEngine;

namespace InterchangeReloader.Ops;

public static class ReflectionUtils
{
    public static float RestockCar(IOpsCar car, Load? load)
    {
        if (car is not OpsCarAdapter adapter)
        {
            return 0.0F;
        }
        
        var type = typeof(OpsCarAdapter);
        var carVar = type.GetField("_car", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter) as Car;
        var loadSlots = type.GetField("_loadSlots",  BindingFlags.Instance | BindingFlags.NonPublic).GetValue(adapter) as List<LoadSlot>;

        float usedQuantity = 0.0f;
        
        for (int i = 0; i < loadSlots.Count; ++i)
        {
            LoadSlot slot = loadSlots[i];
            CarLoadInfo? loadInfo = carVar.GetLoadInfo(i);
            if (loadInfo.HasValue)
            {
                carVar.SetLoadInfo(i, null);
            }

            if (load == null)
            {
                continue;
            }

            if (usedQuantity == 0.0F && slot.LoadRequirementsMatch(load) && slot.LoadUnits == load.units)
            {
                usedQuantity = Mathf.CeilToInt(load.Pounds(slot.MaximumCapacity) / 2000f);
                CarLoadInfo newInfo = new CarLoadInfo(load.id, slot.MaximumCapacity);
                carVar.SetLoadInfo(i, newInfo);
            }
        }

        return usedQuantity;
    }
}