using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Combustion of CO with O2 by formula 2CO + O2 -> 2CO2
/// </summary>
[UsedImplicitly]
public sealed partial class COCombustionReaction : IGasReactionEffect
{
    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0;

        var temperatureScale = 0f;

        if (temperature > Atmospherics.COUpperTemperature)
        {
            temperatureScale = 1f;
        } else
        {
            temperatureScale = (temperature - Atmospherics.COMinimumBurnTemperature)
                / (Atmospherics.COUpperTemperature - Atmospherics.COMinimumBurnTemperature);
        }

        if (temperatureScale > 0)
        {
            var COBurnRate = 0f;

            var initialOxygenMoles = mixture.GetMoles(Gas.Oxygen);
            var initialCOMoles = mixture.GetMoles(Gas.CarbonMonoxide);

            if (initialOxygenMoles > initialCOMoles * Atmospherics.COOxygenFullburn)
            {
                COBurnRate = initialCOMoles * temperatureScale / Atmospherics.COBurnRateDelta;
            } else
            {
                COBurnRate = temperatureScale * (initialOxygenMoles / Atmospherics.COOxygenFullburn) / Atmospherics.COBurnRateDelta;
            }

            if (COBurnRate > Atmospherics.MinimumHeatCapacity)
            {
                COBurnRate = MathF.Min(COBurnRate, MathF.Min(initialCOMoles, initialOxygenMoles * 2f));
                mixture.SetMoles(Gas.CarbonMonoxide, initialCOMoles - COBurnRate);
                mixture.SetMoles(Gas.Oxygen, initialOxygenMoles - (COBurnRate / 2f));

                var currentCO2 = mixture.GetMoles(Gas.CarbonDioxide);
                mixture.SetMoles(Gas.CarbonDioxide, currentCO2 + COBurnRate);


                energyReleased += Atmospherics.FireCOEnergyReleased * COBurnRate;
                energyReleased /= heatScale;
                mixture.ReactionResults[(byte)GasReaction.Fire] += COBurnRate * 2f;
            }

            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
            }

            if (location != null)
            {
                var mixTemperature = mixture.Temperature;
                if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location, mixTemperature, mixture.Volume);
                }
            }
        }
        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }
}
