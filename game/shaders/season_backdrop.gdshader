// YOK-152: the tea house backdrop with its season screens. Each season painting is mapped onto the eight screens'
// bounding box (panel_rect, in backdrop UV) at its own aspect, shifted down so row painting_bottom_v meets the sill, and shown only where the panel mask is white, so the screen frames
// stay painted. The screens show a mix of the four paintings by `weights` (spring, summer, autumn, winter; they sum to 1),
// so a fade can start from whatever is showing (SeasonBeats, tick-driven). A missing painting (present = 0) shows the
// plain backdrop in its share. Unshaded, no fog, like the grove's backdrop.
shader_type spatial;
render_mode unshaded, fog_disabled;

uniform sampler2D backdrop : source_color, filter_linear_mipmap;
uniform sampler2D panel_mask : hint_default_black, filter_linear;
uniform sampler2D season_0 : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D season_1 : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D season_2 : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D season_3 : source_color, filter_linear_mipmap, repeat_disable;
/// The screens' bounding box in backdrop UV: xy = top-left, zw = size (TeaHouseStage.PanelRectUv).
uniform vec4 panel_rect = vec4(0.115625, 0.2527778, 0.76875, 0.3555556);
/// The paintings' row (v) that sits on the screens' bottom edge (TeaHouseStage.PaintingBottomV). Below 1 the paintings
/// shift down, unscaled, so their blank paper strip falls below the sill; the top of the screens repeats the sky in row 0.
uniform float painting_bottom_v = 1.0;
/// Each season's share of the screens, and 1 where its painting exists.
uniform vec4 weights = vec4(1.0, 0.0, 0.0, 0.0);
uniform vec4 present = vec4(0.0);

void fragment() {
	vec3 base = texture(backdrop, UV).rgb;
	vec2 suv = (UV - panel_rect.xy) / panel_rect.zw;
	float inside = step(0.0, suv.x) * step(suv.x, 1.0) * step(0.0, suv.y) * step(suv.y, 1.0);
	float m = texture(panel_mask, UV).r * inside;
	vec2 cuv = clamp(suv - vec2(0.0, 1.0 - painting_bottom_v), vec2(0.0), vec2(1.0));
	vec4 w = weights * present;
	vec3 screens = base * (1.0 - (w.x + w.y + w.z + w.w))
		+ w.x * texture(season_0, cuv).rgb + w.y * texture(season_1, cuv).rgb
		+ w.z * texture(season_2, cuv).rgb + w.w * texture(season_3, cuv).rgb;
	ALBEDO = mix(base, screens, m);
}
