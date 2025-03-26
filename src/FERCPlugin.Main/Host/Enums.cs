
namespace FERCPlugin.Main.Host;
public enum LeftRightTypeEnum {
    left,
    right
}

public enum VentUnitGroupCategoryEnum {
    block,
    arrow,
    utilization,
    utilization_cross,
    utilization_separate,
    end_element
}

public enum VentUnitItemTypeEnum {
    // endComponents
    airValve,
    flexibleDamper,
    visor,

    // baseComponents
    airFilter,
    fan,
    noiseSuppressor,

    // heatCoolComponents
    waterHeater,
    waterCooler,
    freonCooler,
    electricHeater,

    // utilizationComponents
    plateUtilizer,
    rotorUtilizer,
    recyclingCamera,
    interimCoolant,

    // optionalComponents
    multifuncSection,
    steamHumidifier,
    adiabaticHumidifier,
    uvDisinfectant,
    frame,

    ODA,
    ETA,
    SUP,
    EHA,
}

public enum DirectionTypeEnum { axe, up, down, service_side, side }
