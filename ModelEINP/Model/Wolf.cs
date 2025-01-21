using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Components.Layers.Temporal;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using NetTopologySuite.Utilities;

namespace ModelEINP.Model; 

public class Wolf : AbstractAnimal
{
    [ActiveConstructor]
    public Wolf()
    {
    }

    private void TestHerd()
    {
        Console.WriteLine(HerdId);
    }

    private const bool Logger = true;
    private static PackManager _packManager;

    
    [ActiveConstructor] 
    public Wolf(
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
        Position position) : 
        base(landscapeLayer, 
            perimeter,
            vegetationLayer,
            waterLayer,
            rasterWaterLayer,
            id,
            animalType,
            isLeading,
            herdId,
            latitude, 
            longitude,
            position)
    {
        SetTestingValues();
        InitPack();
    }

    private void SetTestingValues()
    {
        IsLeading = true;
        HerdId = 1;
        Age = 3;
        AnimalType = Random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
    }

    #region Properties and Fields
    
    protected override double Hydration { get; set; } = MaxHydration;
    protected override double Satiety { get; set; } = MaxSatiety;
    public override Position Position { get; set; }
    public override Position Target { get; set; }
    [PropertyDescription(Name = "Latitude")]
    public override double Latitude { get; set; }
    [PropertyDescription(Name = "Longitude")]
    public override double Longitude { get; set; }
    
    [PropertyDescription(Name = "IsLeading")]
    public override bool IsLeading { get; set; }
    [PropertyDescription (Name="HerdId")] 
    public override int HerdId { get; set; }

    public AbstractAnimal HuntingTarget = null;
    public double BearingToPrey;
    private Position LastPosition;
    public readonly object WolfLock = new object();
    private static readonly object CircleLock = new object();
    
    private WolfPack Pack { get; set; }
    
    #endregion

    #region Constants
    private readonly Dictionary<AnimalLifePeriod, double> _starvationRate = new()
    {
        /*
         * Initial value is per Hour
         * The Daily Food Need is taken into account at CalculateSatietyFactor(Animal)
         * Assumption: A Wolf can survive with eating every 3 days
         * --> Verliert jede Stunde ein 3*18 tel, weil durch die Nacht in Summe 18 Stunden pro Tag abgezogen werden
         */
        { AnimalLifePeriod.Calf, MaxSatiety / (3 * 18) }, 
        { AnimalLifePeriod.Adolescent, MaxSatiety / (3 * 18) }, 
        { AnimalLifePeriod.Adult, MaxSatiety / (3 * 18) }  
    };
    
    //total need of food per day in kilograms
    [PropertyDescription]
    public static double DailyFoodAdult { get; set; }
    [PropertyDescription]
    public static double DailyFoodPup { get; set; } 
    [PropertyDescription]
    public static double DailyFoodAdolescent { get; set; }
    
    //simulation / behavior parameters
    [PropertyDescription]
    public static double HungryThreshold { get; set; }
    [PropertyDescription]
    
    //distance is per hour
    public static double MaxHuntDistanceInM { get; set; }
    [PropertyDescription]
    public static int PregnancyDurationInDays { get; set; }
    
    [PropertyDescription]
    public static int MinLitterSize { get; set; }
    [PropertyDescription]
    public static int MaxLitterSize { get; set; }
    [PropertyDescription]
    public static int PupSurvivalRate { get; set; }
    [PropertyDescription]
    public static int SafeDistanceToPrey { get; set; }
    

    [PropertyDescription] 
    public static double RunningSpeedInMs { get; set; }

    //Chance for an adult female animal to become pregnant per year in percent
    //rate needs to be adjusted with data
    private const int ChanceForPregnancy = 75;
    
    #endregion
    
    public override void Tick()
    {
        if (IsFirstTick)
        {
            FirstTick();
            IsFirstTick = false;
            return;
        }
        
        TicksLived++;
        Pack = _packManager.FindById(HerdId);
        
        //check for pregnancy duration and eventually give birth
        if (Pregnant) {
            if (TicksToDays(PregnancyDurationInTicks) < PregnancyDurationInDays) {
                if(Logger) Console.WriteLine("Wolf: " + ID + "is pregnant");
                PregnancyDurationInTicks++;
            }
            else {
                PregnancyDurationInTicks = 0;
                var list = new List<Wolf>();
                var litterSize = Random.Next(MinLitterSize, MaxLitterSize);
                for (var i = 0; i < litterSize; i++)
                {
                    var wolf = LandscapeLayer.SpawnWolf(LandscapeLayer, Perimeter, VegetationLayer, VectorWaterLayer, RasterWaterLayer,
                        AnimalType.WolfPup, false, HerdId, Latitude, Longitude, Position);
                    list.Add(wolf);
                }
                _packManager.FindById(HerdId).InsertNewborns(list);
                if(Logger) Console.WriteLine("Wolf: " + ID + "gave birth");
            }
        }
        
        //check for yearly event
        if (TicksToDays(TicksLived) >= 365)
        {
            YearlyRoutine();
        }
        if (!IsAlive) return;
        
        //daily life
        if (Satiety < HungryThreshold)
        {
            if (TickLengthInSec > 90)
            {
               EasyHuntAndSearch();
            }
            else
            {
                DetailedHunting();
            }
        }
        else
        {
            LastPosition = Position;
            DoRandomWalk(10);
        }

        //only males look actively for a partner, not for ecological reasons, but for simpler simulation
        if (IsLookingForPartner() && AnimalType == AnimalType.WolfMale) LookForPartner();

        UpdateState();
        
    }

    public override void FirstTick()
    {
        LastPosition = Position;
        CalculateParams();
        lock (PackManager.PackChangingLock)
        {
            InitPack();
        }
    }

    public override void CalculateParams()
    {
        //Calculating Movements per Tick
        RunDistancePerTick = RunningSpeedInMs * TickLengthInSec;
        RandomWalkMaxDistanceInM = (int)Math.Round ((RandomWalkMaxDistanceInM / (double) 3600) * TickLengthInSec);
        RandomWalkMinDistanceInM = (int)Math.Round ((RandomWalkMinDistanceInM / (double) 3600) * TickLengthInSec);
        
        // Recalculation of dehydration and starvation rates 
        var keys = _starvationRate.Keys.ToList();
        foreach (var key in keys)
        {
            _starvationRate[key] = (_starvationRate[key] / 3600) * TickLengthInSec;
        }

        if (TickLengthInSec > 24 * 60 * 60) HungryThreshold = 101;
    }

    /// <summary>
    /// updates time dependent fields
    /// </summary>
    /// <exception cref="NullReferenceException"></exception>
    protected override void UpdateState()
    {
        
        int currentHour;
        if (LandscapeLayer.Context.CurrentTimePoint != null)
            currentHour = LandscapeLayer.Context.CurrentTimePoint.Value.Hour;
        else
            throw new NullReferenceException();
        
        // adjust satiety
        if (currentHour is >= 21 and <= 23 or >= 0 and <= 4 ) {   
            BurnSatiety(_starvationRate[LifePeriod] / 4); //less food is consumed while sleeping
        }
        else
        {
            BurnSatiety(_starvationRate[LifePeriod]);
        }
        if (Satiety > 100.0) Satiety = 100;
        
        //starve
        if (Satiety <= 0.0)
        {
            Die(MattersOfDeath.NoFood);
            if(Logger) Console.WriteLine("A Wolf starved");
        }
    }

    public override void YearlyRoutine()
    {
        TicksLived = 0;
        Age++;

        //new LifePeriod
        var newLifePeriod = GetAnimalLifePeriodFromAge(Age);
        if (newLifePeriod != LifePeriod)
        {
            if (newLifePeriod == AnimalLifePeriod.Adolescent)
            {
                AnimalType = AnimalType.WolfYearling;
                //Pups have a high chance of dying in the first year
                if (Random.Next(100) > PupSurvivalRate)
                {
                    Pack.LeavePack(this);
                    Die(MattersOfDeath.Natural);
                    if(Logger) Console.WriteLine("A Pup died");
                }
            }
            
            if (newLifePeriod == AnimalLifePeriod.Adult)
            {
                //decide sex, 50:50 chance of being male or female
                AnimalType = Random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
                if(Logger) Console.WriteLine("Wolf: " + ID + "grew up and is: " + AnimalType + " and isLeading: " + IsLeading);

            }
            LifePeriod = newLifePeriod;
        }

        //33% chance to leave the pack and found a new one if adult and not the leader
        if (!IsLeading && LifePeriod == AnimalLifePeriod.Adult)
        {
            if (Random.Next(3) == 0) _packManager.StartNewPack(this);
        }
        
        //reproduction
        if (!(Age >= ReproductionYears[0] && Age <= ReproductionYears[1])) return;
        
        //only mother wolf can get pregnant when a father is present, todo: check for distance
        if (!AnimalType.Equals(AnimalType.WolfFemale) || !IsLeading) return;
        if(Logger) Console.WriteLine("Wolf: " + AnimalType + " HerdId: " + HerdId + " is leading: "+ IsLeading);

        if (Pack?.Father is not null
            && LifePeriod == AnimalLifePeriod.Adult
            && Random.Next(100) < ChanceForPregnancy-1) 
        {
                if(Logger) Console.WriteLine("Wolf: " + ID + "got pregnant");
                Pregnant = true;
        }
        
    }

    public override AnimalLifePeriod GetAnimalLifePeriodFromAge(int age)
    {
        if (age < 1) return AnimalLifePeriod.Calf;
        return age <= 2 ? AnimalLifePeriod.Adolescent : AnimalLifePeriod.Adult;
    }

    public override double SatietyFactor()
    {
        Console.Error.WriteLine("Tried to eat a wolf");
        throw new InvalidOperationException();
    }

    private bool IsLookingForPartner()
    {
        if (LifePeriod != AnimalLifePeriod.Adult) return false;
        if (Pack.Father == this && Pack.Mother is null) return true;
        if (Pack.Mother == this && Pack.Father is null) return true;
        return false;
    }

    public void GiveFood(double food)
    {
        var need = LifePeriod switch
        {
            AnimalLifePeriod.Adult => DailyFoodAdult,
            AnimalLifePeriod.Adolescent => DailyFoodAdolescent,
            _ => DailyFoodPup
        };
        Satiety += (food * 100) / need;
    }
    
    private void LookForPrey()
    {
        if (this == Pack.GetLeader())
        {
            if(Logger) Console.WriteLine("Looking for prey");
            DoRandomRoam(4);
        }
        else
        {
            if(Logger) Console.WriteLine("Following on search for prey");
            var leader = Pack.GetLeader();
            DoRandomFollow(leader.Position, 5);
        }
        //Todo: find target with explore
        var target = LandscapeLayer.Environment.GetNearest(Position, MaxHuntDistanceInM, IsAnimalPrey);
        if (target is not null)
        {
            if (Logger) Console.WriteLine("Found prey to hunt");
            Pack.StartHunt(target);
        }
    }
    
    /// <summary>
    /// Hunting with a set success rate for longer TickLength. Looking for prey in an area depending on config and TickLength.
    /// On Success, Prey is killed and shared with pack to update satiety
    /// </summary>
    private void EasyHuntAndSearch()
    {
        AbstractAnimal target = null;
        //recalculating the distance based on tick length, for realism should scale less than linear with TickLength
        if (HuntingTarget is not null)
        {
            if (HuntingTarget.Position.DistanceInMTo(Position) <= ((MaxHuntDistanceInM / 3600) * TickLengthInSec))
                target = HuntingTarget;
        }
        if (target is null)
        {
            LookForPrey();
            return;
        }
        
        //success rate could be improved in many ways
        if (Random.Next(100) > 50) return;
        
        Pack.ShareKill(target);
        target.Die(MattersOfDeath.Culling);
        if(Logger) Console.WriteLine("Found prey: " + target.ID + "  and eaten");
    }

    /// <summary>
    /// Executes movement during a hunt with low Tick Length.
    /// Behaviour depends on pack and prey and own object attributes.
    /// On Success, Prey is killed and shared with pack to update satiety
    /// </summary>
    private void DetailedHunting() {
        if (IsPartOfHunt)
        {
            CalculateTarget();
            Position = Target;
            Pack.EncirclingList.UpdateList();
            //check for success only for one wolf per Tick, whole pack must be there, then static kill rate (needs to be improved)
            if (this == Pack.GetLeader() && Pack.EncirclingList.Length() == Pack.Members.Count && Random.Next(100) > 15)
            {
                Pack.ShareKill(HuntingTarget);
                HuntingTarget.Die(MattersOfDeath.Culling);
                Pack.EndHunt();
            }
            //Todo: Huntingmaxduration and cooldown after hunting end
        }
        else
        {
            LookForPrey();
        }
    }
    

    private void CalculateTarget()
    {
        var preyPosition = HuntingTarget.Position;
        var distLeft = RunDistancePerTick;
        //Console.WriteLine(ID + ": is distance away: " + Position.DistanceInMTo(preyPosition));
        if (Position.DistanceInMTo(preyPosition) > SafeDistanceToPrey + 0.01 && !Pack.EncirclingList.Contains(this))
        {
            //Console.WriteLine(ID + ": has to catch up");
            //catch up to safe distance
            var bearing = Position.GetBearing(preyPosition);

            var runDistance = RunDistancePerTick;
            var distToCover = Position.DistanceInMTo(preyPosition);
            if (distToCover < RunDistancePerTick)
            {
                runDistance = distToCover - SafeDistanceToPrey;
                //Console.WriteLine(ID + ": should now be on circle, runDistance= " + runDistance);
            }
            
            Target = Position.GetRelativePosition(bearing, runDistance);

            var newDist = Target.DistanceInMTo(preyPosition);

            // if too close move away
            if (newDist > SafeDistanceToPrey + 0.01) return;
            Target = preyPosition.GetRelativePosition(InvertBearing(bearing), SafeDistanceToPrey);
            SetBearingToPrey(bearing);
            Pack.EncirclingList.Register(this);
        }
        else
        {
            lock (CircleLock)
            {
                //find spot on circle
                var left = Pack.EncirclingList.GetLeft(this);
                var right = Pack.EncirclingList.GetRight(this);

                if (left is not null)
                {
                    if (right is null)
                    {
                        var oppositeBearing = left.Position.GetBearing(preyPosition);
                        Target = preyPosition.GetRelativePosition(oppositeBearing, SafeDistanceToPrey);
                        SetBearingToPrey(InvertBearing(oppositeBearing));
                        return;
                    }

                    //Console.WriteLine("2 neighbours found");

                    //Find middle between both neighbours
                    var fullDist = left.Position.DistanceInMTo(right.Position);
                    var bear = left.Position.GetBearing(right.Position);
                    var middle = left.Position.GetRelativePosition(bear, fullDist / 2);

                    // check if swap is needed
                    var bearDiffLeft = preyPosition.GetBearing(left.Position) -
                                       Position.GetBearing(left.Position);
                    var bearDiffRight = preyPosition.GetBearing(right.Position) -
                                        Position.GetBearing(right.Position);

                    //Calculate bearing
                    var bearingFromPrey = middle.GetBearing(preyPosition);

                    //invert if all three wolfs are on one half of the circle
                    if (Math.Abs(bearDiffRight) + Math.Abs(bearDiffLeft) > 180)
                        bearingFromPrey = InvertBearing(bearingFromPrey);

                    Target = preyPosition.GetRelativePosition(bearingFromPrey, SafeDistanceToPrey);

                    var distToTarget = Position.DistanceInMTo(Target);
                    //if (distToTarget > distLeft) Target = Position.GetRelativePosition(Position.GetBearing(Target), distLeft);
                    
                    SetBearingToPrey(InvertBearing(bearingFromPrey));
                }
            }
        }
    }

    /// <summary>
    /// Moves towards a position. The movement vector varies by up to -20m to +10m in length and +-15 degrees in direction, to simulate randomness in the pack movements.
    /// If no try is successful the movement will end excactly on the target position
    /// </summary>
    /// <param name="targetPosition"> position to follow </param>
    /// <param name="numOfAttempts"> number of random attempts to get a valid target position </param>
    private void DoRandomFollow(Position targetPosition, int numOfAttempts)
    {
        var dist =Position.DistanceInMTo(targetPosition);
        var bearing = Position.GetBearing(targetPosition);
        var far = true;
        var newDist = dist;
        
        if (dist < RandomWalkMaxDistanceInM)
        {
            far = false;
            newDist += Random.Next(-100, 50) / 2.0;
        }
        else
        {
            newDist = RandomWalkMaxDistanceInM;
        }

        //try until a valid EndPosition is found
        while (numOfAttempts > 0)
        {
            var dirOffset = (double) Random.Next(0, (int)30) - 15; //angle should depend on TickLength to keep distance smaller on slower tickrates
            if (far) dirOffset = dirOffset / 7;
            var newDir = bearing + dirOffset;
            
            Target = Position.GetRelativePosition(newDir, newDist);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                Position = Target;
                break;
            }
            numOfAttempts--;
        }
        //if all failed (cant be true if break was hit) go to exact same spot
        if (numOfAttempts == 0) Position = targetPosition;
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }

    /// <summary>
    /// Moves towards a random position that is "forward". The angle will be randomly generated to change between -90 and +90 degrees.
    /// If no try is successfull, the wolf wilkl "turn around" and the same will be done in the opposite direction.
    /// If no try is successful again, the wolf will go back the way he came and the movement will end excactly on the last position
    /// </summary>
    /// <param name="numOfAttempts"> number of random attempts to get a valid target position </param>
    private void DoRandomRoam(int numOfAttempts)
    {
        var bearing = LastPosition.GetBearing(Position);
        var retry = true;
        var temp = numOfAttempts;
        
        //try until a valid EndPosition is found
        while (numOfAttempts > 0)
        {
            var randomDistance = Random.Next(RandomWalkMinDistanceInM, RandomWalkMaxDistanceInM);
            var randomDirOffset = (double) (Random.Next(0, 100) - 50) / 2.5;
            if (!retry) randomDirOffset = randomDirOffset * 18.0;
            
            var newDir = bearing + randomDirOffset;
            
            Target = Position.GetRelativePosition(newDir, randomDistance);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                LastPosition = Position;
                Position = Target;
                break;
            }
            numOfAttempts--;
            //if all failed turn around and try again
            if (numOfAttempts == 0 && retry)
            {
                retry = false;
                bearing = InvertBearing(bearing);
                numOfAttempts = temp;
            }
        }
        
        //if all failed again (cant be true if break was hit) go back to the exact same spot
        if (numOfAttempts == 0) Position = LastPosition;
        Console.WriteLine("Tries left: " + numOfAttempts);
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }
    
    /// <summary>
    /// Only males should do this. Looks for a partner in an area depending on config and TickLength
    /// If found Packs are updated. Still could need Thread safety
    /// </summary>
    private void LookForPartner()
    {
        if(Logger) Console.WriteLine("Looking for a partner");
        //recalculating the distance based on tick length, for realism should scale less than linear with TickLength
        var target = LandscapeLayer.Environment.GetNearest(Position, (MaxHuntDistanceInM / 3600) * TickLengthInSec, IsPossibleFemalePartner);
        if (target is null or Wolf) return;
        
        // Cast is safe because of check in previous line
        // ReSharper disable once PossibleInvalidCastException
        Pack.Mother = (Wolf) target;
        target.HerdId = HerdId;
        target.IsLeading = true;
        
        if(Logger) Console.WriteLine("Found partner: " + target.ID + "  and started family");
    }

    //checks if animal is possible prey
    private static bool IsAnimalPrey(AbstractAnimal animal)
    {
        return animal.AnimalType switch
        {
            AnimalType.BisonCow 
                or AnimalType.BisonBull 
                or AnimalType.BisonCalf 
                or AnimalType.BisonNewborn
                or AnimalType.ElkCow 
                or AnimalType.ElkBull 
                or AnimalType.ElkCalf 
                or AnimalType.ElkNewborn
                or AnimalType.MooseCow 
                or AnimalType.MooseBull 
                or AnimalType.MooseCalf
                or AnimalType.MooseNewborn 
                => true,
            
            _ => false
        };
    }

    private static bool IsPossibleFemalePartner(AbstractAnimal animal)
    {
        if (animal.AnimalType != AnimalType.WolfFemale) return false;
        //casting is safe because of previous check for animalType
        var wolf = (Wolf) animal;
        return wolf.IsLookingForPartner();
    }
    
    public int CompareTo(object obj)
    {
        if (obj is not Wolf wolf) return -1;
        if (this.Equals(wolf)) return 0;
        return BearingToPrey.CompareTo(wolf.BearingToPrey);
    }

    private double InvertBearing(double bearing)
    {
        if (bearing is > 360 or < 0) Console.WriteLine("Somethings not right with the bearing");
        if(bearing > 180 ) return bearing - 180;
        if (bearing <= 180) return bearing + 180;
        return -1;
    }
    
    public void SetBearingToPrey(double bearing)
    {
        lock (WolfLock)
        {
            BearingToPrey = bearing;
        }
    }
    
    private void InitPack()
    {
        _packManager ??= new PackManager();
        var pack = _packManager.FindById(HerdId);
        if (pack is null)
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var members = new List<Wolf>();
            members.Add(this);
            pack = _packManager.CreateWolfPack(HerdId, null, null, members);
        }
        else
        {
            pack.Members.Add(this);
        }

        if (IsLeading)
        {
            if (AnimalType == AnimalType.WolfMale) pack.Father = this;
            if(AnimalType == AnimalType.WolfFemale) pack.Mother = this;
        }
    }
}