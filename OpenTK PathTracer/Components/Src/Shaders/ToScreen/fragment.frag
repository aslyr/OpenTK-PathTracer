#version 450 core
layout(location = 0) out vec4 FragColor;

layout(binding = 0) uniform sampler2D SamplerTexture;
layout(binding = 1) uniform sampler2D SamplerRasterizer;

vec3 LessThan(vec3 f, float value);
vec3 LinearToSRGB(vec3 rgb);
vec3 SRGBToLinear(vec3 rgb);
vec3 ACESFilm(vec3 x);

layout (location = 0) uniform float DEBUG;

in vec2 TexCoord;
void main()
{
    vec3 color = texture(SamplerTexture, TexCoord).rgb;
    color += texture(SamplerRasterizer, TexCoord).rgb;

    color = ACESFilm(color);
    color = LinearToSRGB(color);
    
    FragColor = vec4(color, 1.0); 
}

vec3 LessThan(vec3 f, float value)
{
    return vec3(
        (f.x < value) ? 1.0 : 0.0,
        (f.y < value) ? 1.0 : 0.0,
        (f.z < value) ? 1.0 : 0.0);
}

vec3 LinearToSRGB(vec3 rgb)
{
    rgb = clamp(rgb, 0.0, 1.0);
     
    return mix(
        pow(rgb, vec3(1.0 / 2.4)) * 1.055 - 0.055,
        rgb * 12.92,
        LessThan(rgb, 0.0031308)
    );
}
 
vec3 SRGBToLinear(vec3 rgb)
{
    rgb = clamp(rgb, 0.0, 1.0);
     
    return mix(
        pow(((rgb + 0.055) / 1.055), vec3(2.4)),
        rgb / 12.92,
        LessThan(rgb, 0.04045)
    );
}

// ACES tone mapping curve fit to go from HDR to LDR
//https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
vec3 ACESFilm(vec3 x)
{
    float a = 2.51;
    float b = 0.03;
    float c = 2.43;
    float d = 0.59;
    float e = 0.14;
    return clamp((x * (a * x + b)) / (x * (c * x + d) + e), 0.0, 1.0);
}