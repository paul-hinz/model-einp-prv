using System;
using System.Collections.Generic;

namespace ModelEINP.Model;

//TODO: Use this and implement a PackManager with nextID, findById and Factory
public class WolfPack(int packId, Wolf father, Wolf mother, List<Wolf> other)
{
    private List<Wolf> _members = other;
    
    private int Id { get; } = packId;
    public Wolf Father { get; } = father;
    public Wolf Mother { get; } = mother;

    public void InsertNewborns(List<Wolf> newBorns)
    {
        foreach (Wolf wolf in newBorns)
        {
            _members.Add(wolf);
        }
        
    }
    
}