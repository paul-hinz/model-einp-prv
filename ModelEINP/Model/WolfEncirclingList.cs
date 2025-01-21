using System.Collections.Generic;

namespace ModelEINP.Model;

public class WolfEncirclingList
{
    private readonly object _lockObject = new object();
    private readonly List<Wolf> _members = new List<Wolf>();

    public Wolf GetLeft(Wolf wolf)
    {
        if (_members.Count == 1) return null;
        var ownInd = _members.IndexOf(wolf);
        if (ownInd < 0) return null;
        if (ownInd == _members.Count - 1) return _members[0];
        return _members[ownInd + 1];
    }
    
    public Wolf GetRight(Wolf wolf)
    {
        if (_members.Count <= 2) return null;
        var ownInd = _members.IndexOf(wolf);
        if (ownInd == 0) return _members[^1];
        if (ownInd < 0) return null;
        return _members[ownInd - 1];
    }

    public void Register(Wolf wolf)
    {
        _members.Add(wolf);
        UpdateList();
    }

    public void UpdateList()
    {
        lock (_lockObject)
        {
            _members.Sort((a, b) =>
            {
                lock (a.WolfLock)
                lock (b.WolfLock)
                {
                    return a.BearingToPrey.CompareTo(b.BearingToPrey);
                }
            });
        }
    }

    public int Length()
    {
        return _members.Count;
    }

    public bool Contains(Wolf wolf)
    {
        return _members.Contains(wolf);
    }
}