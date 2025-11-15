shader_type canvas_item;

uniform sampler2D tex_y;

uniform sampler2D tex_u;

uniform sampler2D tex_v;

uniform mat3 color_space_matrix;

// Manual adjustments
uniform float saturation : hint_range(0.0, 200.0, 1.0) = 100.0;

uniform float lightness  : hint_range(0.0, 100.0, 1.0) = 50.0;

uniform float contrast   : hint_range(-100.0, 100.0, 1.0) = 0.0;

uniform float hue        : hint_range(0.0, 360.0, 1.0) = 0.0;

// Tint color
uniform vec4 tint_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// Padding
uniform vec2 texture_scale = vec2(1.0, 1.0); // = vec2(width / stride, 1.0)

// Chroma key settings
uniform bool chroma_key_enable = false;

uniform vec3 chroma_key_color : source_color = vec3(0.0, 1.0, 0.0); // Default green

uniform float chroma_key_threshold : hint_range(0.0, 1.0, 0.01) = 0.4;

uniform float chroma_key_smoothness : hint_range(0.0, 1.0, 0.01) = 0.1;

const float EPSILON = 0.00001;

// Helper: convert RGB to HSV
vec3 rgb2hsv(vec3 c)
{
    float maxc = max(c.r, max(c.g, c.b));

    float minc = min(c.r, min(c.g, c.b));

    float delta = maxc - minc;

    float h = 0.0;

    if (delta > EPSILON)
    {
        if (abs(maxc - c.r) < EPSILON)
            h = mod(((c.g - c.b) / delta), 6.0);
        else if (abs(maxc - c.g) < EPSILON)
            h = ((c.b - c.r) / delta) + 2.0;
        else
            h = ((c.r - c.g) / delta) + 4.0;
	
        h /= 6.0;
    }

    float s = (maxc <= EPSILON) ? 0.0 : (delta / maxc);

    float v = maxc;

    return vec3(h, s, v);
}

// Helper: convert HSV back to RGB
vec3 hsv2rgb(vec3 c)
{
    float h = c.x * 6.0;

    float s = c.y;

    float v = c.z;

    int i = int(floor(h));

    float f = h - float(i);

    float p = v * (1.0 - s);

    float q = v * (1.0 - s * f);

    float t = v * (1.0 - s * (1.0 - f));

    if (i == 0) 
        return vec3(v, t, p);

    if (i == 1) 
        return vec3(q, v, p);

    if (i == 2) 
        return vec3(p, v, t);

    if (i == 3) 
        return vec3(p, q, v);

    if (i == 4) 
        return vec3(t, p, v);

    return vec3(v, p, q);
}

// Calculate chroma key alpha based on color distance
float calculate_chroma_alpha(vec3 rgb, vec3 key_col, float threshold, float smoothness)
{
    // Calculate color distance
    float dist = distance(rgb, key_col);
    
    // Smoothstep for smooth edges
    float alpha = smoothstep(threshold - smoothness, threshold + smoothness, dist);
    
    return alpha;
}

void fragment() 
{
    vec2 uv = UV;

    // Scale UV base on stride
    vec2 y_uv = uv * texture_scale;

    vec2 uv_uv = uv * texture_scale;
    
    float y_sample = texture(tex_y, y_uv).r;

    float u_sample = texture(tex_u, uv_uv).r;

    float v_sample = texture(tex_v, uv_uv).r;

    float Y, U, V;

    // LIMITED RANGE (16-235/240) - Standard for video
    Y = (y_sample * 255.0 - 16.0) / 219.0;   // Normalize 16-235 to 0-1

    U = (u_sample * 255.0 - 128.0) / 224.0;  // Normalize 16-240 to -0.5 - 0.5

    V = (v_sample * 255.0 - 128.0) / 224.0;  // Normalize 16-240 to -0.5 - 0.5

    vec3 yuv = vec3(Y, U, V);

    vec3 rgb = color_space_matrix * yuv;

    // Clamp to valid range after conversion
    rgb = clamp(rgb, 0.0, 1.0);

    // Contrast (-100..100 → 0..2)
    float c = (contrast + 100.0) / 100.0;

    // Lightness (0..100 → -1..1)
    float l = (lightness / 50.0) - 1.0;

    rgb = (rgb - 0.5) * c + 0.5 + l;

    // Saturation (0..200 → 0..2 scale)
    float s = saturation / 100.0;

    float luma = dot(rgb, vec3(0.299, 0.587, 0.114));

    rgb = mix(vec3(luma), rgb, s);

    // Hue shift
    if (abs(hue) > 0.001) 
    {
        vec3 hsv = rgb2hsv(rgb);

        hsv.x = fract(hsv.x + hue / 360.0);

        rgb = hsv2rgb(hsv);
    }

    // Apply tint
    rgb *= tint_color.rgb;

    // Final clamp
    rgb = clamp(rgb, 0.0, 1.0);

    // Calculate chroma key alpha
    float alpha = tint_color.a;

    if (chroma_key_enable)
    {
        float chroma_alpha = calculate_chroma_alpha(rgb, chroma_key_color, chroma_key_threshold, chroma_key_smoothness);
        alpha *= chroma_alpha;
    }

    COLOR = vec4(rgb, alpha);
}