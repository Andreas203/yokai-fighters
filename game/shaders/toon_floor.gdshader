// YOK-39: the bamboo grove floor. The ground strip is cropped out of its letterbox by UV (art_rect) and
// mirror-tiled (the strip isn't seamless), then calmed toward the muted V1 palette. Toon lit like the fighters.
shader_type spatial;
render_mode diffuse_toon, specular_toon;

uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_enable;
/// The art inside the PNG's grey border, in UV: xy = top-left, zw = size.
uniform vec4 art_rect = vec4(0.0, 0.0, 1.0, 1.0);
/// Tiles across the floor (x) and in depth (y).
uniform vec2 tiles = vec2(1.0, 1.0);
uniform vec3 tint =vec3(0.78, 0.74, 0.70);
/// How far the strip is pulled toward grey (0 = raw colour).
uniform float desaturate : hint_range(0.0, 1.0) = 0.35;

void fragment() {
	vec2 t = UV * tiles;
	vec2 uv = 1.0 - abs(mod(t, 2.0) - 1.0); // mirrored tiling
	vec2 a = art_rect.xy + uv * art_rect.zw;
	vec2 gx = dFdx(t) * art_rect.zw, gy = dFdy(t) * art_rect.zw;
	// Keep the filter footprint off the grey border at every mip level (no seams where the tiles mirror).
	vec2 size = vec2(textureSize(tex, 0));
	float lod = max(0.0, log2(max(length(gx * size), length(gy * size))));
	vec2 pad = 1.5 * exp2(ceil(lod)) / size;
	a = clamp(a, art_rect.xy + pad, art_rect.xy + art_rect.zw - pad);
	vec3 c = textureGrad(tex, a, gx, gy).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	ALBEDO = mix(c, vec3(l), desaturate) * tint;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
