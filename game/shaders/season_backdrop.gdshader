// YOK-152: the tea house backdrop with its season screens. The season painting's UV 0-1 is remapped onto the eight
// screens' bounding box (panel_rect, in backdrop UV) and shown only where the panel mask is white, so the screen frames
// stay painted. Two paintings cross-fade by blend (SeasonScreens, tick-driven). Unshaded, no fog, like the grove's backdrop.
shader_type spatial;
render_mode unshaded, fog_disabled;

uniform sampler2D backdrop : source_color, filter_linear_mipmap;
uniform sampler2D panel_mask : hint_default_black, filter_linear;
uniform sampler2D season_from : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D season_to : source_color, filter_linear_mipmap, repeat_disable;
/// The screens' bounding box in backdrop UV: xy = top-left, zw = size (TeaHouseStage.PanelRectUv).
uniform vec4 panel_rect = vec4(0.115625, 0.2527778, 0.76875, 0.3555556);
/// 1 where that painting exists (else the plain backdrop shows in its place).
uniform float use_from = 0.0;
uniform float use_to = 0.0;
uniform float blend : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	vec3 base = texture(backdrop, UV).rgb;
	vec2 suv = (UV - panel_rect.xy) / panel_rect.zw;
	float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
	float m = texture(panel_mask, UV).r * inside;
	vec2 cuv = clamp(suv, vec2(0.0), vec2(1.0));
	vec3 a = mix(base, texture(season_from, cuv).rgb, use_from);
	vec3 b = mix(base, texture(season_to, cuv).rgb, use_to);
	ALBEDO = mix(base, mix(a, b, blend), m);
}
