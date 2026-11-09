// YOK-151: a backdrop used as the FAR view only (the riverbank). Only art_rect of the painting is shown (its upper rows:
// sky, hills, far shore, far water), filling the quad below the top `sky` fraction; above the art the painting's top row
// carries on (the sky continues, so the quad can be taller than the art for raised and wide framings). The painted water
// (image v below water_v, except its warm lanterns) is pulled toward water_tint at the same lightness, so it meets the 3D
// water's indigo with no seam.
// Unshaded, no fog, like the grove's backdrop.
shader_type spatial;
render_mode unshaded, fog_disabled;

uniform sampler2D tex : source_color, filter_linear_mipmap, repeat_disable;
/// The part of the image shown, in image UV: xy = top-left, zw = size.
uniform vec4 art_rect = vec4(0.0, 0.0, 1.0, 1.0);
/// Fraction of the quad's height above the art (the sky carried on from the art's top row).
uniform float sky = 0.0;
/// Image v from which the painting shows water, and the tint pulled over it (same lightness).
uniform float water_v = 1.0;
uniform vec3 water_tint : source_color = vec3(0.176, 0.227, 0.369);
uniform float water_tint_amount : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	float v = clamp((UV.y - sky) / (1.0 - sky), 0.0, 1.0);
	vec2 img = art_rect.xy + vec2(UV.x, v) * art_rect.zw;
	// Keep the filter off the rows outside the art (and off the top edge where the sky carries on).
	vec2 size = vec2(textureSize(tex, 0));
	img.y = clamp(img.y, art_rect.y + 1.0 / size.y, art_rect.y + art_rect.w - 1.0 / size.y);
	vec3 c = textureLod(tex, img, 0.0).rgb;
	vec3 lum = vec3(0.299, 0.587, 0.114);
	vec3 tinted = water_tint * (dot(c, lum) / max(dot(water_tint, lum), 1e-3));
	// The painted lanterns (warm: red well above blue) keep their colour; only the water itself is tinted.
	float water = smoothstep(water_v, water_v + 0.006, img.y) * (1.0 - smoothstep(0.04, 0.12, c.r - c.b));
	ALBEDO = mix(c, tinted, water * water_tint_amount);
}
