using UnityEngine;

public static class JointExtensions
{
    public static ConfigurableJoint CloneFrom(this ConfigurableJoint target, ConfigurableJoint source)
    {
        if (source == null || target == null)
            return target;

        // Basic settings
        target.anchor = source.anchor;
        target.axis = source.axis;
        target.connectedAnchor = source.connectedAnchor;
        target.autoConfigureConnectedAnchor = source.autoConfigureConnectedAnchor;
        target.secondaryAxis = source.secondaryAxis;

        // Connected body
        target.connectedBody = source.connectedBody;

        // Motions
        target.xMotion = source.xMotion;
        target.yMotion = source.yMotion;
        target.zMotion = source.zMotion;
        target.angularXMotion = source.angularXMotion;
        target.angularYMotion = source.angularYMotion;
        target.angularZMotion = source.angularZMotion;

        // Limits
        target.linearLimit = source.linearLimit;
        target.lowAngularXLimit = source.lowAngularXLimit;
        target.highAngularXLimit = source.highAngularXLimit;
        target.angularYLimit = source.angularYLimit;
        target.angularZLimit = source.angularZLimit;

        // Drives
        target.xDrive = source.xDrive;
        target.yDrive = source.yDrive;
        target.zDrive = source.zDrive;
        target.angularXDrive = source.angularXDrive;
        target.angularYZDrive = source.angularYZDrive;
        target.slerpDrive = source.slerpDrive;

        // Springs & Projection
        target.linearLimitSpring = source.linearLimitSpring;
        target.angularXLimitSpring = source.angularXLimitSpring;
        target.angularYZLimitSpring = source.angularYZLimitSpring;
        target.projectionAngle = source.projectionAngle;
        target.projectionDistance = source.projectionDistance;
        target.projectionMode = source.projectionMode;

        // Misc
        target.breakForce = source.breakForce;
        target.breakTorque = source.breakTorque;
        target.enableCollision = source.enableCollision;
        target.enablePreprocessing = source.enablePreprocessing;
        target.massScale = source.massScale;
        target.connectedMassScale = source.connectedMassScale;

        return target;
    }
}