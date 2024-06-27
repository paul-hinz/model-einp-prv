using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;

namespace ModelEINP.Model;

public class PackManager
{
    private int _nextId = 1;
    private List<WolfPack> _packs = new List<WolfPack>();

    private void UpdateNextId(int newId)
    {
        if (newId >= _nextId) _nextId = newId + 1;
    }
    
    public int NextId()
    {
        return _nextId++;
    }

    public WolfPack CreateWolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
    {
        var wolfPack = new WolfPack(packId, father, mother, other);
        _packs.Add(wolfPack);
        Console.WriteLine($"Pack with Id: {packId} created");
        UpdateNextId(packId);
        return wolfPack;
    }

    public WolfPack CreateWolfPack(Wolf loneWolf)
    {
        var packId = NextId();
        var wolfPack = new WolfPack(packId, loneWolf, null, new List<Wolf>());
        _packs.Add(wolfPack);
        return wolfPack;
    }

    public WolfPack FindById(int packId)
    {
        return _packs.FirstOrDefault(p => p.GetId() == packId);
    }
}
