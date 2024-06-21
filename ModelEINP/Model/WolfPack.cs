using System;
using System.Collections.Generic;

namespace ModelEINP.Model;

public class WolfPack
{
    private List<Wolf> _members;
    private int Id { get; }
    public Wolf Father { get; set;}
    public Wolf Mother { get; set; }

    internal WolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
    {
        Id = packId;
        Father = father;
        Mother = mother;
        _members = other ?? new List<Wolf>();
    }

    public void InsertNewborns(List<Wolf> newborns)
    {
        foreach (Wolf wolf in newborns)
        {
            _members.Add(wolf);
        }
        
    }

    public int GetId()
    {
        return Id;
    }
    
}