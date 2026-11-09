// YOK-151: the riverbank's near swamp water, from the bank all the way back to the painting. Dark pine-green near the bank
// (behind the fighters, darker and greener than the Kappa's teal shell, so the shell parts from it), easing with distance
// into the painted water's colour at the join (far_color at far_z, the backdrop's plane), so there is no seam. Teal only as
// thin, faint, broken ripple lines that fade out where they would crowd toward the horizon. All a pure function of the world
// position (no time): flat cel colours, unshaded and fog-free like the backdrop it meets, no reflections, no glow. Palette:
// pine #34483B, ink #1D1B21, teal #3E7F86; far water #2B4140 (the painting's).
shader_type spatial;
render_mode unshaded, fog_disabled;

uniform vec3 near_color : source_color = vec3(0.149, 0.176, 0.275);
uniform vec3 far_color : source_color = vec3(0.133, 0.196, 0.290);
uniform vec3 teal : source_color = vec3(0.243, 0.498, 0.525);
/// World z where the near colour starts easing, and where the water meets the painting.
uniform float near_z = -9.0;
uniform float far_z = -40.0;
/// How far the ripple lines lean toward teal, lines per metre of depth and their width in metres.
uniform float ripple_mix = 0.4;
uniform float ripple_density = 0.9;
uniform float ripple_width = 0.05;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(float n) {
	return fract(sin(n * 91.3458) * 47453.5453);
}

void fragment() {
	vec2 p = world.xz;
	float t = clamp((near_z - p.y) / (near_z - far_z), 0.0, 1.0);
	vec3 c = mix(near_color, far_color, t * t * (3.0 - 2.0 * t));
	float r = (p.y + 0.12 * sin(p.x * 0.7)) * ripple_density;
	float f = fract(r);
	float d = min(f, 1.0 - f) / ripple_density; // metres to the nearest line
	float aa = fwidth(r) / ripple_density;
	float line = 1.0 - smoothstep(ripple_width * 0.5, ripple_width * 0.5 + aa, d);
	float row = floor(r + 0.5);
	float dash = step(0.55, fract(p.x * (0.08 + 0.05 * hash(row)) + hash(row + 3.0)));
	float fade = clamp(1.0 - fwidth(r) * 4.0, 0.0, 1.0); // gone where lines come closer than a few pixels
	c = mix(c, teal, line * dash * ripple_mix * fade);
	ALBEDO = c;
}
