// YOK-151: the riverbank's water strip between the bank and the backdrop. Flat deep indigo (the painted water at the
// seam) with a few broad teal bands and thin, broken ripple lines, all a pure function of the world position (no time):
// flat cel colours, unshaded like the backdrop so it meets the painted water, no reflections, no glow.
shader_type spatial;
render_mode unshaded;

uniform vec3 deep : source_color = vec3(0.12, 0.17, 0.26);
uniform vec3 teal : source_color = vec3(0.243, 0.498, 0.525);
/// How far the broad bands and the ripple lines lean toward teal.
uniform float band_mix = 0.18;
uniform float ripple_mix = 0.45;
/// Ripple lines per metre of depth and their width (fraction of a line period).
uniform float ripple_density = 0.9;
uniform float ripple_width = 0.06;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(float n) {
	return fract(sin(n * 91.3458) * 47453.5453);
}

void fragment() {
	vec2 p = world.xz;
	float band = step(0.72, 0.5 + 0.5 * sin(p.y * 1.1 + 0.8 * sin(p.x * 0.23)));
	vec3 c = mix(deep, teal, band * band_mix);
	float t = (p.y + 0.12 * sin(p.x * 0.7)) * ripple_density;
	float line = 1.0 - step(ripple_width, fract(t));
	float row = floor(t);
	// Broken lines: each row shows in a few dashes of its own length and offset.
	float dash = step(0.55, fract(p.x * (0.08 + 0.05 * hash(row)) + hash(row + 3.0)));
	c = mix(c, teal, line * dash * ripple_mix);
	ALBEDO = c;
}
