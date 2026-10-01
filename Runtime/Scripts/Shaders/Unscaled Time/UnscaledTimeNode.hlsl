#ifndef UNSCALED_TIME_NODE_INCLUDED
#define UNSCALED_TIME_NODE_INCLUDED

float4 _UnscaledTimeParams; 

void UnscaledTime_float(out float OutTime, out float OutSineTime, out float OutCosineTime, out float OutDeltaTime, out float OutSmoothDelta)
{
    OutTime = _UnscaledTimeParams.x;
    OutSineTime = sin(_UnscaledTimeParams.x);
    OutCosineTime = cos(_UnscaledTimeParams.x);
    OutDeltaTime = _UnscaledTimeParams.y;
    OutSmoothDelta = _UnscaledTimeParams.z;
}

void UnscaledTime_half(out half OutTime, out half OutSineTime, out half OutCosineTime, out half OutDeltaTime, out half OutSmoothDelta)
{
    OutTime = (half)_UnscaledTimeParams.x;
    OutSineTime = (half)sin(_UnscaledTimeParams.x);
    OutCosineTime = (half)cos(_UnscaledTimeParams.x);
    OutDeltaTime = (half)_UnscaledTimeParams.y;
    OutSmoothDelta = (half)_UnscaledTimeParams.z;
}
#endif