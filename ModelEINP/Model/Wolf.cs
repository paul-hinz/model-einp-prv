using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
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
            if (TickLengthInSec > 300)
            {
               LookForPreyAndHunt();
            }
            else
            {
                DetailedHunting();
            }
        }
        else
        {
            DoRandomWalk(10);
        }

        //only males look actively for a partner, not for ecological reasons, but for simpler simulation
        if (IsLookingForPartner() && AnimalType == AnimalType.WolfMale) LookForPartner();

        UpdateState();
        
    }

    public override void FirstTick()
    {
        if (LandscapeLayer.Context.StartTimePoint is not null) 
            LastDate = LandscapeLayer.Context.StartTimePoint.Value.Date;
        CalculateParams();
        InitPack();
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
        if (Satiety == 0.0)
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
    
    /// <summary>
    /// Hunting with a set success rate for longer TickLength. Looking for prey in an area depending on config and TickLength.
    /// On Success, Prey is killed and shared with pack to update satiety
    /// </summary>
    private void LookForPreyAndHunt()
    {
        if(Logger) Console.WriteLine("Looking for prey");
        //recalculating the distance based on tick length, for realism should scale less than linear with TickLength
        var target = LandscapeLayer.Environment.GetNearest(Position, (MaxHuntDistanceInM / 3600) * TickLengthInSec, IsAnimalPrey);
        if (target is null) return;
        
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
            //ToDo: cooperate with pack for hunt, wie Erfolg feststellen
        }
        else
        {
            if (this == Pack.GetLeader())
            {
                DoRandomWalk(10);
                var target = LandscapeLayer.Environment.GetNearest(Position, MaxHuntDistanceInM, IsAnimalPrey);
                if (target is not null) Pack.StartHunt(target);
            }
            else
            {
                var leader = Pack.GetLeader();
                DoRandomFollow(leader.Position, 10);
            }
        }
    }

    /// <summary>
    /// Moves towards a position. The movement vector varies by 15% in length and 15 degrees in direction, to simulate randomness in the pack movements.
    /// If no try is successful the movement will end excactly on the target position
    /// </summary>
    /// <param name="targetPosition"> position to follow </param>
    /// <param name="numOfAttempts"> number of random attempts to get a valid target position </param>
    private void DoRandomFollow(Position targetPosition, int numOfAttempts)
    {
        var dist =Position.DistanceInMTo(targetPosition);
        var bearing = Position.GetBearing(targetPosition);
        
        //try until a valid EndPosistion is found
        while (numOfAttempts > 0) {
            var distFactor = (double) Random.Next(85, 116) / 100; // maybe try an absolute offset instead, e.g. -10 to 10 meters
            var dirOffset = Random.Next(0, 30) - 15;

            var newDist = dist * distFactor; 
            var newDir = bearing + dirOffset;
            
            Target = Position.GetRelativePosition(newDir, newDist);
            
            if (Perimeter.IsPointInside(Target) && !RasterWaterLayer.IsPointInside(Target)) {
                Position = Target;
                break;
            }
            numOfAttempts--;
        }
        
        Assert.IsTrue(Perimeter.IsPointInside(Position) && !RasterWaterLayer.IsPointInside(Position));
    }

    /// <summary>
    /// Only males should do this. Looks for a partner in an area depending on config and TickLength
    /// If found Packs are updated. Still needs Thread safety 
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
            if(AnimalType == AnimalType.WolfMale) pack.Father = this;
            if(AnimalType == AnimalType.WolfFemale) pack.Mother = this;
        }
    }
}