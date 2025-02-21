using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mars.Common;
using Mars.Common.Core.Collections;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using NetTopologySuite.Utilities;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace ModelEINP.Model; 

public class Wolf : AbstractAnimal
{
    [ActiveConstructor]
    public Wolf()
    {
    }

    private const bool Logger = false;
    
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
        InitNextID();
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
    public override double Satiety { get; set; } = MaxSatiety;
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

    //fields for hunting
    private AbstractAnimal HuntingTarget;
    private double BearingToPrey;
    private Position LastPosition;
    private bool IsOnCircle;

    private static int NextID = 1;
    private bool IsLookingForPartner { get; set; }
    
    #endregion

    #region Constants
    private readonly Dictionary<AnimalLifePeriod, double> _starvationRate = new()
    {
        /*
         * Initial value is per Hour
         * The Daily Food Need is taken into account at CalculateSatietyFactor(Animal)
         * Assumption: A Wolf can survive with eating every 20 days
         * --> Verliert jede Stunde ein 10*18 tel, weil durch die Nacht in Summe 18 Stunden pro Tag abgezogen werden
         */
        { AnimalLifePeriod.Calf, MaxSatiety / (20 * 18) }, 
        { AnimalLifePeriod.Adolescent, MaxSatiety / (20 * 18) }, 
        { AnimalLifePeriod.Adult, MaxSatiety / (20 * 18) }  
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
    
    //distance is per second
    public static double MaxHuntDistanceInM { get; set; }
    [PropertyDescription]
    
    public static double VisionRangeInM { get; set; }
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

    ///Chance for an adult female animal to become pregnant per year in percent. 
    ///Rate needs to be adjusted with data
    private const int ChanceForPregnancy = 75;
    
    ///Table for hunting success rates in percent. Each row represents 1 more wolf but starting with 0. First column for elks, second for bigger prey. 
    ///e.g. 5 wolves hunting a moose should invoke [5][1] and thus return 5 percent. 
    ///Could be parametrized with config
    private readonly int[,] HuntingRates = new int[16, 2]
    {
        {0,0},
        {14, 1},
        {18, 2},
        {25, 3},
        {33, 4},
        {28, 5},
        {25, 7},
        {21, 9},
        {17, 12},
        {15, 16},
        {13, 21},
        {11, 28},
        {10, 24},
        {8, 21},
        {7, 17},
        {6, 15}
    };
    private bool isDetailed;
    
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
        
        //check for pregnancy duration and eventually give birth
        if (Pregnant) {
            if (TicksToDays(PregnancyDurationInTicks) < PregnancyDurationInDays) {
                if(Logger) Console.WriteLine("Wolf: " + ID + "is pregnant");
                PregnancyDurationInTicks++;
            }
            else {
                PregnancyDurationInTicks = 0;
                Pregnant = false;
                var litterSize = Random.Next(MinLitterSize, MaxLitterSize);
                for (var i = 0; i < litterSize; i++)
                {
                    var pup = LandscapeLayer.SpawnWolf(LandscapeLayer, Perimeter, VegetationLayer, VectorWaterLayer, RasterWaterLayer,
                        AnimalType.WolfPup, false, HerdId, Latitude, Longitude, Position);
                }
                if(Logger) Console.WriteLine("Wolf: " + ID + " gave birth");
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
            if (isDetailed)
            { 
                DetailedHunting();
            }
            else
            { 
                EasyHuntAndSearch(); 
            }
        }
        else
        {
            LastPosition = Position.Copy();
            DoRandomWalk(10);
        }

        //only males look actively for a partner, not for ecological reasons, but for simpler simulation
        if (IsLookingForPartner && AnimalType == AnimalType.WolfMale) LookForPartner();

        UpdateState();
        
    }
    
    public override void FirstTick()
    {
        LastPosition = Position.Copy();
        CalculateParams();
        InitNextID();
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

        isDetailed = TickLengthInSec < 60;

        //hunt always if TickLength > 1 day
        if (TickLengthInSec > 24 * 60 * 60) HungryThreshold = 101;
        
        //pups dont need correction on initialization or are later spawned
        if(AnimalType is AnimalType.WolfPup) return;
        
        //initialize leader as adults, others as yearlings
        if (IsLeading)
        {
            LifePeriod = AnimalLifePeriod.Adult;
            Age = 3;
        }
        else
        {
            LifePeriod = AnimalLifePeriod.Adolescent;
            Age = 1;
            AnimalType = AnimalType.WolfYearling;
        }
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
                    Die(MattersOfDeath.Natural);
                    if(Logger) Console.WriteLine("A Pup died");
                }
            }
            
            if (newLifePeriod == AnimalLifePeriod.Adult)
            {
                //Yearlings have a high chance of dying as well
                if (Random.Next(100) > PupSurvivalRate * 3)
                {
                    Die(MattersOfDeath.Natural);
                    if(Logger) Console.WriteLine("A Pup died");
                }
                //decide sex, 50:50 chance of being male or female
                AnimalType = Random.Next(2) == 0 ? AnimalType.WolfMale : AnimalType.WolfFemale;
                if(Logger) Console.WriteLine("Wolf: " + ID + " grew up and is: " + AnimalType + " and isLeading: " + IsLeading);

            }
            LifePeriod = newLifePeriod;
        }

        //33% chance to leave the pack and found a new one if adult and not the leader
        if (!IsLeading && LifePeriod == AnimalLifePeriod.Adult)
        {
            if (Random.Next(3) == 0)
            {
                HerdId = GetNextID();
                if (Logger) Console.WriteLine("Leaving a pack and starting a new one with ID: " + HerdId);
                IsLeading = true;
                IsLookingForPartner = true;
            }
            
        }
        
        //reproduction
        if (!(Age >= ReproductionYears[0] && Age <= ReproductionYears[1])) return;
        
        //only mother wolf can get pregnant when a father is present 
        if (AnimalType.Equals(AnimalType.WolfFemale) && IsLeading)
        {

            //find a partner (must be male, same pack and leading)
            var partner = LandscapeLayer.Environment.Explore(Position, -1D, 1,
                animal => animal is Wolf wolf && wolf.HerdId == this.HerdId && wolf.IsLeading &&
                          wolf.AnimalType.Equals(AnimalType.WolfMale)
            ).FirstOrDefault();

            if (partner is not null
                && LifePeriod == AnimalLifePeriod.Adult
                && Random.Next(100) < ChanceForPregnancy)
            {
                if (Logger) Console.WriteLine("Wolf: " + ID + " got pregnant");
                Pregnant = true;
            }
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

    /// <summary>
    /// Improves satiety value dependend on given food and daily need
    /// </summary>
    /// <param name="food"> nutritional value (in kgs)</param>
    private void GiveFood(double food)
    {
        var need = LifePeriod switch
        {
            AnimalLifePeriod.Adult => DailyFoodAdult,
            AnimalLifePeriod.Adolescent => DailyFoodAdolescent,
            _ => DailyFoodPup
        };
        Satiety += (food * 100) / need;
    }
    
    /// <summary>
    /// Simulates movement through the park in search for prey. Depends on situation and role in pack
    /// </summary>
    private void LookForPrey(List<AbstractAnimal> packList)
    {
        Wolf leader = null;
        
        foreach (var w in packList.Cast<Wolf>().Where(w => w.IsLeading))
        {
            if (w.AnimalType.Equals(AnimalType.WolfFemale) && leader is null)
            {
                leader = w;
            }
            if(w.AnimalType.Equals(AnimalType.WolfMale))
            {
                leader = w;
            }
        }
        
        leader ??= this;
        
        if (this == leader)
        {
            if (HuntingTarget is null)
            {
                if (Logger) Console.WriteLine("Looking for prey");
                DoRandomRoam(4);
            }
            else
            {
                DoRandomFollow(HuntingTarget.Position, 5);
            }
        }
        else
        {
            if(Logger && HuntingTarget is null) Console.WriteLine("Following on search for prey");
            DoRandomFollow(leader.Position, 5);
        }
        
        if(HuntingTarget is not null) return;

        double range;
        if (isDetailed)
        {
            range = VisionRangeInM;
        }
        else
        {
            //only scaling with square root, so the possible area scales linear with tick length
            range = MaxHuntDistanceInM * Math.Sqrt(TickLengthInSec);
        }
        //May need synchronization to ensure, that every pack member hunts the same animal
        //Priorities young and then weak (low satiety) prey animals
        var target = LandscapeLayer.Environment.Explore(Position, range, -1, IsAnimalPrey)
            .OrderBy(a => a.LifePeriod)
            .ThenBy(a => a.Satiety)
            .FirstOrDefault();
        if (target is not null && HuntingTarget is null)
        {
            lock (target.AnimalChangingLock)
            {
                if (Logger) Console.WriteLine("Found prey to hunt");
                foreach (var w in packList.Cast<Wolf>())
                {
                    w.HuntingTarget = target;
                }
            }
        }
    }

    /// <summary>
    /// Hunting with a set success rate for longer TickLength. Looking for prey in an area depending on config and TickLength.
    /// On Success, Prey is killed and shared with pack to update satiety
    /// </summary>
    /// <param name="packList1"></param>
    private void EasyHuntAndSearch()
    {
        List<AbstractAnimal> packList;
        try
        {
            packList = LandscapeLayer.Environment.Explore(Position, -1D, -1,
                animal => animal is Wolf && animal.HerdId == HerdId
            ).WhereNotNull().ToList();
        }
        catch(NullReferenceException)
        {
            Console.Error.WriteLine("HuntAndSearch failed because of NullReference during packList Explore");
            return;
        }
        catch(IndexOutOfRangeException)
        {
            Console.Error.WriteLine("HuntAndSearch failed because of IndexOutOfRange during packList Explore");
            return;
        }

        LookForPrey(packList);

        if (HuntingTarget is null) return;
        
        Wolf leader = null;
        
        foreach (var w in packList.Cast<Wolf>().Where(w => w.IsLeading))
        {
            if (w.AnimalType.Equals(AnimalType.WolfFemale) && leader is null)
            {
                leader = w;
            }
            if(w.AnimalType.Equals(AnimalType.WolfMale))
            {
                leader = w;
            }
        }
        
        leader ??= this;
        if (this != leader) return;
        
        
        //success rate could be improved in many ways
        var packSize = packList.Count - 1;
        if (packSize > 15) packSize = 15;
        if (HuntingTarget is null) return;
        var factor = 1;
        if (TickLengthInSec > 18000) factor = 3;
        
        if (Random.Next(100) >= HuntingRates[packSize, IsBigPrey(HuntingTarget)] * factor)
        {
            return;
        }
        
        lock (AnimalChangingLock)
        {
            //multiple checks for null because of parallelism
            if (HuntingTarget is null) return;

            //lock to ensure a target can only be killed and eaten once
            lock (HuntingTarget.AnimalChangingLock)
            {
                if (!HuntingTarget.IsAlive)
                {
                    HuntingTarget = null;
                }

                if (HuntingTarget is null)
                {
                    return;
                }

                HuntingTarget.Die(MattersOfDeath.Culling);
                if (Logger) Console.WriteLine("Found prey: " + HuntingTarget.ID + "  and eaten");
                ShareKill(packList, HuntingTarget.SatietyFactor(), HuntingTarget.Position);
            }
        }
    }

    /// <summary>
    /// Executes movement during a hunt with low Tick Length.
    /// Behaviour depends on pack and prey and own object attributes.
    /// On Success, Prey is killed and shared with pack to update satiety
    /// </summary>
    private void DetailedHunting()
    {
        var packList = LandscapeLayer.Environment.Explore(Position, -1D, -1,
            animal => animal is Wolf && animal.HerdId == HerdId
        ).WhereNotNull().ToList();
        
        if (HuntingTarget is not null && HuntingTarget.IsAlive)
        {
            CalculateTarget(packList);
            JumpTo(Target);
            
            //whole pack must be there, then static kill rate, depending on pack size (needs to be improved)
            if (packList.OfType<Wolf>().All(wolf => wolf.IsOnCircle))
            {
                
                Wolf leader = null;
                foreach (var w in packList.Cast<Wolf>().Where(w => w.IsLeading))
                {
                    if (w.AnimalType.Equals(AnimalType.WolfFemale) && leader is null)
                    {
                        leader = w;
                    }
                    if(w.AnimalType.Equals(AnimalType.WolfMale))
                    {
                        leader = w;
                    }
                }
                leader ??= this;
            
                //success rate could be improved in many ways
                var packSize = packList.Count - 1;
                if (packSize > 15) packSize = 15;
                if (this == leader && Random.Next(100) < HuntingRates[packSize, IsBigPrey(HuntingTarget)])
                {
                    lock (HuntingTarget.AnimalChangingLock)
                    {
                        if (HuntingTarget.IsAlive)
                        {
                            ShareKill(packList, HuntingTarget.SatietyFactor(), HuntingTarget.Position);
                            HuntingTarget.Die(MattersOfDeath.Culling);
                            IsOnCircle = false;
                            if (Logger) Console.WriteLine("Found prey: " + HuntingTarget.ID + "  and eaten");
                            HuntingTarget = null;
                        }
                    }
                }
            }
            //could be improved by a MaxDuration and a possible cooldown after a failed hunt
        }
        else
        {
            LookForPrey(packList);
        }
    }
    

    /// <summary>
    /// Helping method to calculate the position needed for this individual during detailed pack hunts.
    /// </summary>
    /// <param name="packList"> A list of the whole participating pack</param>
    private void CalculateTarget(List<AbstractAnimal> packList)
    {
        var preyPosition = HuntingTarget.Position;
        var distLeft = RunDistancePerTick;
        if (Position.DistanceInMTo(preyPosition) > SafeDistanceToPrey + 0.01 &&
            !packList.All(animal => animal is Wolf wolf && wolf.IsOnCircle))
        {
            //catch up to safe distance
            var bearing = Position.GetBearing(preyPosition);

            var runDistance = RunDistancePerTick;
            var distToCover = Position.DistanceInMTo(preyPosition);
            if (distToCover < RunDistancePerTick)
            {
                runDistance = distToCover - SafeDistanceToPrey;
            }
            
            Target = Position.GetRelativePosition(bearing, runDistance);

            var newDist = Target.DistanceInMTo(preyPosition);

            // if too close move away
            if (newDist > SafeDistanceToPrey + 0.01) return;
            Target = preyPosition.GetRelativePosition(InvertBearing(bearing), SafeDistanceToPrey);
            BearingToPrey = bearing;
            IsOnCircle = true;
        }
        else
        {
            //snapshot to sort based on one configuration, ignoring other movements in the same tick
            var snapshot = packList
                .WhereNotNull()
                .Where(animal => animal is Wolf wolf && wolf.IsOnCircle)
                .Select(wolf => new { Wolf = (Wolf)wolf, Bearing = ((Wolf)wolf).BearingToPrey }) 
                .OrderBy(item => item.Bearing) 
                .ToList();

            if (snapshot.Count < 2) return;
            
            var currentItem = snapshot.FirstOrDefault(item => item.Wolf == this);
            if (currentItem == null) {
                Console.Error.WriteLine("this wolf not found");
                return; //should never happen, but to be safe
            }

            int index = snapshot.IndexOf(currentItem);

            var previousWolf = snapshot[(index - 1 + snapshot.Count) % snapshot.Count].Wolf;
            var nextWolf = snapshot[(index + 1) % snapshot.Count].Wolf;
            
            var rightPosition = previousWolf.Position;
            var leftPosition = nextWolf.Position;
            
            if (snapshot.Count == 2) rightPosition = null;
            
            if (rightPosition is null)
            {
                var oppositeBearing = leftPosition.GetBearing(preyPosition);
                Target = preyPosition.GetRelativePosition(oppositeBearing, SafeDistanceToPrey);
                BearingToPrey = InvertBearing(oppositeBearing);
                return;
            }
            
            double bearingFromPrey;
            
            //cases when all 4 agents are on nearly the same line can lead to inaccuracies and mistakes in bearing calculations
            var normalizedBearing = (BearingToPrey + 180) % 180;
            var prevNormalized = (previousWolf.BearingToPrey + 180) % 180;
            var nextNormalized = (nextWolf.BearingToPrey + 180) % 180;

            if (Math.Abs(normalizedBearing - prevNormalized) < 2 &&
                Math.Abs(normalizedBearing - nextNormalized) < 2)
            {
                bearingFromPrey = (BearingToPrey + 80) % 360;
            }
            else
            {
                //To find the bearing from prey in middle between neighbours:
                //Calculate bearing between those neighbours and middle point to prey is exactly 90 degrees from that
                bearingFromPrey = (360 + leftPosition.GetBearing(rightPosition) + 90) % 360;
            }

            Target = preyPosition.GetRelativePosition(bearingFromPrey, SafeDistanceToPrey);

            var distToTarget = Position.DistanceInMTo(Target);
            //if (distToTarget > distLeft) Target = Position.GetRelativePosition(Position.GetBearing(Target), distLeft);

            BearingToPrey = InvertBearing(bearingFromPrey);
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
            newDist += Random.Next(-100, 50) / 8.0;
        }
        else if (dist > RandomWalkMaxDistanceInM * 3)
        {
            newDist = RandomWalkMaxDistanceInM * 2;
        }
        else
        {
            newDist = RandomWalkMaxDistanceInM * 0.85;
        }

        //try until a valid EndPosition is found
        while (numOfAttempts > 0)
        {
            var dirOffset = (double) Random.Next(0, 30) - 15; //angle should depend on TickLength to keep distance smaller on slower tick rates
            if (far) dirOffset = dirOffset / 7;
            var newDir = bearing + dirOffset;
            
            Target = Position.GetRelativePosition(newDir, newDist);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target))
            {
                JumpTo(Target);
                break;
            }
            numOfAttempts--;
        }
        //if all failed (cant be true if break was hit) go to exact same spot
        if (numOfAttempts == 0) JumpTo(targetPosition);
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }

    /// <summary>
    /// Moves towards a random position that is "forward". The angle will be randomly generated to change between -90 and +90 degrees.
    /// If no try is successful, the wolf will "turn around" and the same will be done in the opposite direction.
    /// If no try is successful again, the wolf will go back the way he came and the movement will end excactly on the last position
    /// </summary>
    /// <param name="numOfAttempts"> number of random attempts to get a valid target position </param>
    private void DoRandomRoam(int numOfAttempts)
    {
        var bearing = LastPosition.GetBearing(Position);
        //there seems to be a bug so that lastposition isnt properly updated. Then the bearing would be allways 0, north, which would slowly move all wolves to the northern edege.
        if (bearing == 0) bearing = Random.Next(360); //if that bug is fixed, this shouldnt be necessary
        
        var retry = true;
        var temp = numOfAttempts;
        //try until a valid EndPosition is found
        while (numOfAttempts > 0)
        {
            var randomDistance = Random.Next(RandomWalkMinDistanceInM, RandomWalkMaxDistanceInM);
            var randomDirOffset = (Random.Next(0, 100) - 50) / 3.0;
            if (!retry) randomDirOffset *= 18.0;
            
            var newDir = bearing + randomDirOffset;
            
            Target = Position.GetRelativePosition(newDir, randomDistance);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                LastPosition = Position.Copy();
                JumpTo(Target);
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
        if (numOfAttempts == 0) JumpTo(LastPosition);
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }
    
    /// <summary>
    /// Only males should do this. (not for ecological reasons) Looks for a partner in an area depending on config and TickLength
    /// Still could need Thread safety
    /// </summary>
    private void LookForPartner()
    {
        if(Logger) Console.WriteLine("Looking for a partner");
        //recalculating the distance based on tick length, only scaling with square root, so the possible area scales linear with tick length
        var target = LandscapeLayer.Environment.Explore(Position, MaxHuntDistanceInM * Math.Sqrt(TickLengthInSec) * 10, 1, 
            animal => animal is Wolf { AnimalType: AnimalType.WolfFemale , IsLookingForPartner: true}).FirstOrDefault();
        if (target is null) return;
        
        // Cast is safe because of predicate in Explore
        // ReSharper disable once PossibleInvalidCastException
        var partner = (Wolf)target;
        partner.IsLookingForPartner = false;
        partner.HerdId = HerdId;
        partner.IsLeading = true;
        
        if(Logger) Console.WriteLine("Found partner: " + target.ID + "  and started family");
    }

    /// <summary>
    /// Shares the nutrition of killed prey equally with all other wolves
    /// </summary>
    /// <param name="pack">a list of the whole pack (including this), must be a List with only wolves</param>
    /// <param name="nutritionValue">the Value of nutrition that should be shared</param>
    /// <param name="position">the position where the food is</param>
    private void ShareKill(List<AbstractAnimal> pack, double nutritionValue, Position position)
    {
        foreach (var w in pack.Cast<Wolf>())
        {
            w.GiveFood(nutritionValue/pack.Count);
            w.IsOnCircle = false;
            w.HuntingTarget = null;
            w.JumpTo(position);
        }
    }

    /// <summary>
    /// checks if animal is possible and living prey
    /// </summary>
    /// <param name="animal">the prey</param>
    private static bool IsAnimalPrey(AbstractAnimal animal)
    {
        if (!animal.IsAlive) return false;
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
    
    /// <summary>
    /// checks if animal is a big prey, mooses and bisons are big, elks are small
    /// </summary>
    /// <param name="animal"> the prey</param>
    /// <returns> 1 if big, 0 otherwise (so should be small)  </returns>
    private static int IsBigPrey(AbstractAnimal animal)
    {
        return animal.AnimalType switch
        {
            AnimalType.BisonCow 
                or AnimalType.BisonBull 
                or AnimalType.BisonCalf 
                or AnimalType.BisonNewborn
                or AnimalType.MooseCow 
                or AnimalType.MooseBull 
                or AnimalType.MooseCalf
                or AnimalType.MooseNewborn 
                => 1,
            
            _ => 0
        };
    }

    /// <summary>
    /// Helping Method to keep bearings positive and avoid math errors
    /// </summary>
    /// <param name="bearing"></param>
    /// <returns> opposite bearing, but as a positive number</returns>
    private double InvertBearing(double bearing)
    {
        if (bearing is > 360 or < 0) Console.WriteLine("Somethings not right with the bearing");
        if(bearing > 180 ) return bearing - 180;
        if (bearing <= 180) return bearing + 180;
        return -1;
    }
    
    public void SetBearingToPrey(double bearing)
    {
        lock (AnimalChangingLock)
        {
            BearingToPrey = bearing;
        }
    }

    
    /// <summary>
    /// 
    /// </summary>
    /// <returns>the next unused id in a thread safe way</returns>
    private int GetNextID()
    {
        return Interlocked.Increment(ref NextID) - 1;
    }

    /// <summary>
    /// Ensures that NextID is initialized in a thread safe way to avoid collisions
    /// </summary>
    private void InitNextID()
    {
        int current, newValue;
        do
        {
            current = NextID;
            newValue = Math.Max(current, HerdId + 1);
        } while (Interlocked.CompareExchange(ref NextID, newValue, current) != current);
    }

}