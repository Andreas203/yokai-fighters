// YOK-150/151/152: a flat procedural floor for the stages without a generated ground strip, toon lit like the fighters
// and the grove floor. pattern 0: two flat tones in soft patches (snow: snow shade + rice paper; bank earth: two browns);
// pattern 1: tatami mats (cell metres, staggered rows) with thin dark seams. Colours are sampled from each backdrop's
// painted floor. back_edge_z (+ a gentle wave) cuts the floor off behind a wavy line: the riverbank's bank edge over the water.
// Everything is a pure function of the world position: no time, no texture.
shader_type spatial;
render_mode diffuse_toon, specular_toon;

uniform int pattern = 0;
uniform vec3 color_a : source_color = vec3(0.76, 0.78, 0.82);
uniform vec3 color_b : source_color = vec3(0.95, 0.91, 0.83);
uniform vec3 seam_color : source_color = vec3(0.29, 0.24, 0.16);
/// Pattern 0: patches per metre, and the share of the floor in color_b.
uniform float patch_scale = 0.3;
uniform float patch_amount = 0.4;
/// Pattern 1: mat size in metres (x across, z in depth) and the seam width in metres.
uniform vec2 cell = vec2(1.8, 0.9);
uniform float seam_width = 0.035;
/// Pattern 1: how much alternate mats darken (straw read of the painted floor).
uniform float mat_shade = 0.05;
/// World z behind which the floor is cut away (riverbank), with this wave amplitude in metres.
uniform float back_edge_z = -1000.0;
uniform float edge_wave = 0.0;
/// A darker rim this many metres deep along the cut edge (wet bank), 0 = none.
uniform float edge_rim = 0.0;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	vec2 p = world.xz;
	float edge = back_edge_z + edge_wave * (sin(p.x * 0.9) + 0.5 * sin(p.x * 2.3 + 1.0));
	if (p.y < edge) discard;
	vec3 c;
	if (pattern == 1) {
		float row = floor(p.y / cell.y);
		float x = p.x / cell.x + 0.5 * mod(row, 2.0);
		vec2 f = vec2(fract(x) * cell.x, fract(p.y / cell.y) * cell.y);
		float d = min(min(f.x, cell.x - f.x), min(f.y, cell.y - f.y));
		// At least about a pixel wide wherever the mats recede (fwidth), so far seams stay solid lines, not dashes.
		float w = max(seam_width * 0.5, fwidth(d) * 0.5);
		float seam = 1.0 - smoothstep(w, w + fwidth(d), d);
		float shade = step(0.5, hash(vec2(floor(x), row))) * mat_shade;
		c = mix(color_a * (1.0 - shade), seam_color, seam);
	} else {
		float n = noise(p * patch_scale) * 0.65 + noise(p * patch_scale * 2.7 + 7.0) * 0.35;
		c = mix(color_a, color_b, step(1.0 - patch_amount, n));
	}
	if (edge_rim > 0.0) c = mix(c * 0.7, c, smoothstep(0.0, edge_rim, p.y - edge));
	ALBEDO = c;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
