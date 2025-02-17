using System;
using System.IO;
using System.Linq;
using Mars.Common;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using NetTopologySuite.Utilities;
using Position = Mars.Interfaces.Environments.Position;
using Mars.Interfaces.Annotations;
using ServiceStack;
// ReSharper disable All

namespace ModelEINP.Model;

public abstract class AbstractAnimal : IPositionable, IAgent<LandscapeLayer> {

    [ActiveConstructor]
    protected AbstractAnimal() {
    }
    
    
    protected AbstractAnimal(
        LandscapeLayer landscapeLayer, 
        Perimeter perimeter,
        VegetationLayer vegetationLayer,
        VectorWaterLayer waterLayer,
        RasterWaterLayer rasterWaterLayer,
        Guid id,
        AnimalType animalType,
        bool isLeading,
        int herdId,
        double latitude, 
        double longitude,
        Position position) { 
        //Position = Position.CreateGeoPosition(longitude, latitude);
        Position = position;
        LandscapeLayer = landscapeLayer;
        Perimeter = perimeter;
        VegetationLayer = vegetationLayer;
        VectorWaterLayer = waterLayer;
        RasterWaterLayer = rasterWaterLayer;
        AnimalType = animalType;
        ID = id;
        IsLeading = isLeading;
        HerdId = herdId;
    }

    #region Properties and Fields

    protected double RunDistancePerTick = 0;
    protected bool IsFirstTick = true;
    public Guid ID { get; set; }
    public abstract Position Position { get; set; }
    public abstract Position Target { get; set; }
    public LandscapeLayer LandscapeLayer { get; set; }
    public abstract double Latitude { get; set; }
    public abstract double Longitude { get; set; }
    public Perimeter Perimeter { get; set; }
    protected abstract double Hydration { get; set; }
    public abstract double Satiety { get; set; }
    public VectorWaterLayer VectorWaterLayer { get; set; }
    public RasterWaterLayer RasterWaterLayer { get; set; }
    public VegetationLayer VegetationLayer { get; set; }

    protected double TickLengthInSec = GlobalValueHelper.TickSeconds;
    protected int TicksLived;
    [PropertyDescription(Name = "_animalType")]
    public AnimalType AnimalType { get; set;}
    protected readonly int[] ReproductionYears = {2, 15};
    protected bool Pregnant;
    protected int PregnancyDurationInTicks;
    protected int ChanceOfDeath;
    protected int Age { get; set; }
    public AnimalLifePeriod LifePeriod;
    public MattersOfDeath MatterOfDeath { get; private set; }
    public bool IsAlive { get; set; } = true;
    
    public abstract bool IsLeading { get; set; }
    public abstract int HerdId { get; set; }
    
    protected static readonly Random Random = new ();

    public readonly object AnimalChangingLock = new object();
    
    #endregion

    #region Constants

    
    [PropertyDescription]
    public int RandomWalkMaxDistanceInM { get; set;  }
    
    [PropertyDescription]
    public int RandomWalkMinDistanceInM { get; set;  }

    protected const double MaxHydration = 100.0;
    protected const double MaxSatiety = 100.0; 
    public const int MaxAge = 25;

    #endregion
    
    public void Init(LandscapeLayer layer) {
        LandscapeLayer = layer;

        var spawnPosition = new Position(Longitude, Latitude);
        LandscapeLayer = layer;
        
        if (Perimeter.IsPointInside(spawnPosition) && !RasterWaterLayer.IsPointInside(spawnPosition)) {
            Position = Position.CreateGeoPosition(Longitude, Latitude);
        } else {
            throw new Exception($"Start point is not valid. Lon: {Longitude}, Lat: {Latitude}, herdID: {HerdId}");
        }
    }
    
    public abstract void Tick();

    public abstract void FirstTick();

    public abstract void CalculateParams();
    
    protected void DoRandomWalk(int numOfAttempts) {
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
        
        while (numOfAttempts > 0) {
            var randomDistance = Random.Next(RandomWalkMinDistanceInM, RandomWalkMaxDistanceInM);
            var randomDirection = Random.Next(0, 360);
            
            Target = Position.GetRelativePosition(randomDirection, randomDistance);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                JumpTo(Target);
                break;
            }
            numOfAttempts--;
        }
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }
    
    protected void LookForWaterAndDrink() {
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
        const int radius = 2000;
        var nearWaterSpots = VectorWaterLayer.Explore(Position.PositionArray, radius)
            .ToList();

        if (nearWaterSpots.Count == 0) return;
        
        var nearestWaterSpot = VectorWaterLayer
            .Nearest(new []{Position.X, Position.Y})
            .VectorStructured
            .Geometry
            .Coordinates
            .Where(coordinate => Perimeter.IsPointInside(new Position(coordinate.X, coordinate.Y)))
            .OrderBy(coordinate => Position.DistanceInMTo(coordinate.X, coordinate.Y))
            .ToList();

        foreach (var point in nearestWaterSpot) {
            Target =  new Position(point.X, point.Y);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                JumpTo(Target);
                Hydration += 20;
                break;
            }
        }
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }

    protected void LookForFoodAndEat() {
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
        if (VegetationLayer.IsPointInside(Position)) {
            var nearVegetationSpots = VegetationLayer.Explore(Position, 20)
                .OrderByDescending(node => node.Node.Value)
                .ToList();

            foreach (var spot in nearVegetationSpots) {
                
                var targetX = spot.Node.NodePosition.X;
                var targetY = spot.Node.NodePosition.Y;

                var targetLon = VegetationLayer.LowerLeft.X +
                                targetX * VegetationLayer.CellWidth;
                var targetLat = VegetationLayer.LowerLeft.Y +
                                targetY * VegetationLayer.CellHeight;

                Target = new Position(targetLon, targetLat);

                if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                    JumpTo(Target);
                    Satiety += 12;
                    break;
                }
            }
        }
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }
    
    //every animal has different ways to consume food or hydration
    protected abstract void UpdateState();

    protected void BurnSatiety(double rate)
    {
        //Max rate is 49, so an animal cant die in less then 3 ticks at longer tick lengths
        if (rate > 49) rate = 49;
        if (Satiety > 0) {
            if (Satiety > rate) {
                Satiety -= rate;
            } else {
                Satiety = 0;
            }
        }
    }

    protected void Dehydrate(double rate)
    {
        if (Hydration > 0) {
            if (Hydration > rate) {
                Hydration -= rate;
            } else {
                Hydration = 0;
            }
        }
    }
    public abstract void YearlyRoutine();

    public abstract AnimalLifePeriod GetAnimalLifePeriodFromAge(int age);

    /// <summary>
    /// calculates the nutritional value if eaten by a wolf
    /// </summary>
    /// <returns>total value of satiety that should be incresed</returns>
    public abstract double SatietyFactor();
    
    
    public void Die(MattersOfDeath matterOfDeath)
    {
        MatterOfDeath = matterOfDeath;
        IsAlive = false;
        LandscapeLayer.RemoveAnimal(LandscapeLayer, this);
    }

    /*
    protected void UpdateDaysLived()
    {
        DateTime currentDate;
        if (LandscapeLayer.Context.CurrentTimePoint != null)
            currentDate = LandscapeLayer.Context.CurrentTimePoint.Value.Date;
        else
            throw new NullReferenceException();
        
        DaysLived += (currentDate - LastDate).Days;
        LastDate = currentDate;
    }
    */

    protected double TicksToDays(int ticks)
    {
        return (ticks * TickLengthInSec) / (60 * 60 * 24);
    }

    protected void JumpTo(Position position)
    {
        LandscapeLayer.Environment.MoveToPosition(this, position.Latitude, position.Longitude);
        Position = position;
    }

}
