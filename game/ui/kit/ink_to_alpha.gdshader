shader_type canvas_item;
// Turns a dark-ink-on-light-paper brush texture (the generated brush-bar.png has no alpha) into a tintable stroke:
// darkness becomes opacity, the node's modulate becomes the ink colour. When a real alpha brush texture replaces it,
// set use_texture_alpha = true (or drop the material): the shader then only tints.
uniform bool use_texture_alpha = false;
uniform float ink_low : hint_range(0.0, 1.0) = 0.42;
uniform float ink_high : hint_range(0.0, 1.0) = 0.78;
varying vec4 tint;

void vertex() { tint = COLOR; }

void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float lum = dot(t.rgb, vec3(0.299, 0.587, 0.114));
	float a = use_texture_alpha ? t.a : 1.0 - smoothstep(ink_low, ink_high, lum);
	COLOR = vec4(tint.rgb, tint.a * a);
}
