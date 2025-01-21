using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;

namespace ModelEINP.Model;

public class PackManager
{
    private int _nextId = 1;
    private readonly List<WolfPack> _packs = new List<WolfPack>();
    public static readonly object PackChangingLock = new object();

    private void UpdateNextId(int newId)
    {
        if (newId >= _nextId) _nextId = newId + 1;
    }

    private int NextId()
    {
        return _nextId++;
    }

    public WolfPack CreateWolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
    {
        var wolfPack = new WolfPack(packId, father, mother, other);
        _packs.Add(wolfPack);
        //Console.WriteLine($"Pack with Id: {packId} created");
        UpdateNextId(packId);
        return wolfPack;
    }

    private int CreateWolfPack(Wolf loneWolf)
    {
        var packId = NextId();
        WolfPack wolfPack;
        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (loneWolf.AnimalType == AnimalType.WolfMale)
        {
            wolfPack = new WolfPack(packId, loneWolf, null, new List<Wolf>());
        }
        else
        {
            wolfPack = new WolfPack(packId, null, loneWolf, new List<Wolf>());
        }
        _packs.Add(wolfPack);
        return packId;
    }

    public WolfPack FindById(int packId)
    {
        return _packs.FirstOrDefault(p => p.GetId() == packId);
    }

    public void StartNewPack(Wolf wolf)
    {
       var pack = FindById(wolf.HerdId);
       pack.LeavePack(wolf);
       wolf.HerdId = CreateWolfPack(wolf);
       wolf.IsLeading = true;
    }
}
