// YOK-finish-vfx-real: the energy layer drawn around every projectile and special-move effect (Fight/Vfx/VfxLayer.cs).
// Presentation only. Additive and HDR (values over 1 feed the stage's glow pass), so the paper-talisman sprite under it
// reads as real energy, fire, water or impact: a soft glow halo, a white-hot core, and procedural particles (embers,
// sparks, droplets, debris, motes) whose positions are pure functions of a hashed index and the sim age.
// Animated ONLY from the sim: `age` is sim ticks since spawn and `life` is 0..1 of the effect's life, both set from
// Match.Tick by the layer, so a replay draws the same frames (F3). No TIME, no random state.
// `shape` = VfxLook (same numbering as vfx.gdshader). The aura quad is `size` times the visual's box, centred on it
// (a ground look keeps its bottom edge on the ground); q is in the visual's height units, +x the way it travels/faces.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled, shadows_disabled, blend_add;

uniform int shape = 0;
uniform vec4 colour : source_color = vec4(1.0);
uniform vec4 core : source_color = vec4(1.0);
uniform float age = 0.0;
uniform float life = 0.0;
uniform float opacity = 1.0;
uniform float flip = 1.0;
uniform float aspect = 1.0;      // the visual's box, width over height
uniform vec2 size = vec2(2.0);   // the aura quad over the visual's box
uniform float ground = 0.0;      // 1: the aura's bottom edge is the visual's bottom edge (ground looks)
uniform float energy = 1.0;      // HDR gain
uniform bool projectile = false;

float h1(float n) { return fract(sin(n * 12.9898 + 0.137) * 43758.5453); }
float vnoise(float x) { float i = floor(x); float f = fract(x); f = f * f * (3.0 - 2.0 * f); return mix(h1(i), h1(i + 1.0), f); }
float blob(vec2 d, float r) { return exp(-dot(d, d) / max(r * r, 1e-5)); }
// a streak from a to b, soft across, thickness w
float streak(vec2 p, vec2 a, vec2 b, float w) {
	vec2 ab = b - a; float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
	vec2 d = p - (a + ab * t);
	return exp(-dot(d, d) / max(w * w, 1e-6)) * (0.35 + 0.65 * t);
}
vec3 vivid(vec3 c, float k) { float l = dot(c, vec3(0.299, 0.587, 0.114)); return max(vec3(0.0), vec3(l) + (c - vec3(l)) * k); }

void fragment() {
	vec2 uv = UV;
	if (flip < 0.0) uv.x = 1.0 - uv.x;
	float hw = aspect * 0.5;
	// q in the visual's height units, centred on the visual's box
	vec2 q = vec2((uv.x - 0.5) * aspect * size.x, (0.5 - uv.y) * size.y);
	if (ground > 0.5) q.y += (size.y - 1.0) * 0.5; // bottom edges match: q.y = -0.5 is the ground
	vec3 hue = vivid(colour.rgb, shape == 2 ? 3.2 : 1.8) + vec3(0.04); // fox fire burns a deeper blue than its pale sprite
	vec3 hot = mix(vec3(1.0), core.rgb, 0.25);
	vec3 col = vec3(0.0);
	float fl = 0.82 + 0.18 * vnoise(age * 0.55) + 0.1 * vnoise(age * 1.7 + 4.0); // flicker, from the sim age
	float fade = projectile ? 1.0 : clamp((1.0 - life) * 1.6, 0.0, 1.0);
	float pop = projectile ? 0.0 : exp(-life * 9.0); // the first frames of a cast or impact flash

	if (shape == 0 || shape == 2 || shape == 5) {
		// FIRE (orb, fox_flame, flame_burst): flickering glow, white-hot heart, licking tongues, embers drifting back and up
		vec2 hc = projectile ? vec2(hw - 0.36, 0.0) : vec2(0.0);
		float rr = projectile ? 0.36 : mix(0.2, 0.55, 1.0 - exp(-life * 5.0));
		vec2 d = q - hc;
		float behind = projectile ? max(0.0, -d.x) : 0.0;
		vec2 e = vec2(d.x + behind * 0.55, d.y * (1.0 + behind * 0.6));
		float halo = blob(e, rr * 2.4 * fl);
		float heart = blob(d, rr * 0.55 * fl);
		// tongues: noisy ridges streaming behind (projectile) or up (burst)
		vec2 tq = projectile ? vec2(-d.x, d.y) : vec2(d.y, d.x);
		float tongues = 0.0;
		for (int k = 0; k < 4; k++) {
			float fk = float(k);
			float off = (fk - 1.5) * 0.14 + 0.08 * sin(tq.x * 7.0 - age * 0.5 + fk * 2.1);
			float len = 0.7 + 0.5 * vnoise(age * 0.4 + fk * 3.3);
			float on = smoothstep(-0.1, 0.05, tq.x) * (1.0 - smoothstep(0.0, len, tq.x));
			tongues += on * exp(-pow((tq.y - off) / (0.07 * (1.0 - tq.x / max(len, 0.01)) + 0.01), 2.0));
		}
		col += hue * (halo * 0.7 + tongues * 0.7) * fl + hot * heart * 0.7;
		// embers / sparks: 28 particles on a loop of 24 ticks, offset by a hashed phase
		for (int k = 0; k < 28; k++) {
			float fk = float(k);
			float ph = fract(h1(fk) + age / (18.0 + 10.0 * h1(fk + 9.0)));
			vec2 v = projectile
				? vec2(-0.9 - 1.6 * h1(fk + 1.0), (h1(fk + 2.0) - 0.5) * 0.9 + 0.5)
				: vec2(cos(fk * 2.4), sin(fk * 2.4)) * (0.5 + 0.9 * h1(fk + 1.0)) + vec2(0.0, 0.3);
			vec2 p = hc + v * ph + vec2(0.05 * sin(ph * 9.0 + fk), 0.0) + vec2(0.0, (h1(fk + 3.0) - 0.5) * 0.3);
			float s = 0.022 * (1.0 - ph) * (0.6 + 0.8 * h1(fk + 4.0));
			float tw = 0.6 + 0.4 * sin(age * 0.9 + fk * 1.7);
			col += mix(hot, hue, 0.3 + 0.7 * ph) * blob(q - p, s * 1.3) * tw * 2.2;
		}
		col += hot * pop * blob(d, 0.6) * 0.8;
	} else if (shape == 1) {
		// SPIRIT WAVE: talisman energy — a glowing crest, a shimmering wake, paper-light motes and streaks trailing
		vec2 cc = vec2(hw - 0.3, 0.0);
		vec2 d = q - cc;
		float crest = exp(-pow((length(d + vec2(0.25, 0.0)) - 0.5) / 0.09, 2.0)) * smoothstep(-0.2, 0.15, d.x + 0.3);
		float behind = max(0.0, -d.x);
		float wake = exp(-pow(d.y / (0.28 + behind * 0.12), 2.0)) * exp(-behind * 1.3) * smoothstep(0.4, -0.1, d.x);
		float shimmer = 0.7 + 0.3 * sin(q.x * 18.0 + age * 0.8) * sin(q.y * 11.0 - age * 0.5);
		col += hue * (crest * 1.1 + wake * 0.9 * shimmer) * fl + hot * blob(d, 0.16) * 0.5;
		for (int k = 0; k < 7; k++) {
			float fk = float(k);
			float ph = fract(h1(fk + 20.0) + age / 14.0);
			float y = (h1(fk + 21.0) - 0.5) * 0.8;
			vec2 a = cc + vec2(-0.2 - ph * 2.2, y);
			col += hue * streak(q, a, a + vec2(0.45, 0.0), 0.018) * (1.0 - ph) * 1.4;
		}
		for (int k = 0; k < 24; k++) {
			float fk = float(k);
			float ph = fract(h1(fk + 40.0) + age / (20.0 + 8.0 * h1(fk + 41.0)));
			vec2 p = cc + vec2(-0.1 - ph * (1.6 + h1(fk + 42.0)), (h1(fk + 43.0) - 0.5) * 0.9 + 0.25 * sin(ph * 6.0 + fk));
			float s = 0.02 * (1.0 - ph * 0.7);
			vec2 r = q - p; float rot = age * 0.2 + fk; r = mat2(vec2(cos(rot), sin(rot)), vec2(-sin(rot), cos(rot))) * r;
			float paper = smoothstep(s, s * 0.4, abs(r.x)) * smoothstep(s * 2.0, s, abs(r.y));
			col += mix(hot, hue, 0.4) * (paper * 1.2 + blob(q - p, s * 2.5) * 0.5) * (1.0 - ph);
		}
	} else if (shape == 3 || shape == 4) {
		// SEAL / IMPACT: a white flash, an expanding shock ring, radial sparks thrown out and slowing
		float r = length(q);
		float R = mix(0.15, 0.75, 1.0 - exp(-life * 4.0));
		float ringw = 0.05 + 0.04 * life;
		col += hue * exp(-pow((r - R) / ringw, 2.0)) * 1.3 * fade;
		col += hue * blob(q, 0.45) * 0.6 * fade + hot * blob(q, 0.22) * (pop * 1.6 + 0.2 * fade);
		for (int k = 0; k < 22; k++) {
			float fk = float(k);
			float a = fk * 2.399 + h1(fk + 60.0);
			vec2 dir = vec2(cos(a), sin(a));
			float sp = 0.6 + 0.9 * h1(fk + 61.0);
			float t = (1.0 - exp(-life * 5.0)) * sp;
			vec2 head = dir * (0.08 + t) + vec2(0.0, -0.25 * life * life);
			vec2 tail = head - dir * (0.18 * (1.0 - life) + 0.02);
			col += mix(hot, hue, life) * streak(q, tail, head, 0.012) * 2.0 * fade;
		}
	} else if (shape == 6) {
		// GROUND POUND (dust): a ground shockwave rolling out, dust billowing, debris thrown in arcs
		float t = 1.0 - exp(-life * 4.0);
		float W = mix(0.3, 1.0, t) * hw * 1.25;
		float gy = -0.5;
		float band = exp(-pow((abs(q.x) - W) / 0.12, 2.0)) * exp(-pow((q.y - gy) / 0.09, 2.0));
		float floorglow = exp(-pow((q.y - gy) / 0.06, 2.0)) * smoothstep(W + 0.1, 0.0, abs(q.x));
		col += hue * (band * 2.2 + floorglow * 0.9) * fade + hot * pop * blob(vec2(q.x * 0.4, q.y - gy), 0.2) * 0.8;
		vec3 dust = mix(core.rgb, vec3(0.62, 0.52, 0.42), 0.6) * 0.42;
		for (int k = 0; k < 14; k++) {
			float fk = float(k);
			float side = h1(fk + 70.0) * 2.0 - 1.0;
			vec2 c = vec2(side * W * (0.6 + 0.4 * h1(fk + 71.0)), gy + 0.08 + life * (0.25 + 0.35 * h1(fk + 72.0)));
			float s = (0.14 + 0.12 * h1(fk + 73.0)) * (0.5 + t);
			col += dust * blob(q - c, s) * fade;
		}
		vec3 rock = mix(hue, vec3(0.35, 0.28, 0.22), 0.65);
		for (int k = 0; k < 26; k++) {
			float fk = float(k);
			float side = h1(fk + 80.0) < 0.5 ? -1.0 : 1.0;
			float vx = side * (0.5 + 1.3 * h1(fk + 81.0));
			float vy = 0.9 + 1.2 * h1(fk + 82.0);
			float x0 = side * W * 0.3 * h1(fk + 83.0);
			float tt = life * 1.1;
			vec2 p = vec2(x0 + vx * tt, gy + vy * tt - 2.4 * tt * tt);
			if (p.y < gy) continue;
			float s = 0.02 + 0.025 * h1(fk + 84.0);
			vec2 r = q - p; float rot = tt * 9.0 * (h1(fk + 85.0) - 0.5) + fk;
			r = mat2(vec2(cos(rot), sin(rot)), vec2(-sin(rot), cos(rot))) * r;
			float chunk = smoothstep(s, s * 0.7, max(abs(r.x), abs(r.y) * 1.4));
			col += (rock * chunk * 0.9 + hue * blob(q - p, s * 2.0) * 0.6) * fade;
		}
	} else if (shape == 7) {
		// CHARGE TRAIL: a blazing streak behind the charger, heat-glow and sparks shed backwards
		float x = (q.x + hw) / (2.0 * hw);
		float body = exp(-pow(q.y / (0.18 + 0.12 * x), 2.0)) * smoothstep(-0.1, 0.8, x) * smoothstep(1.25, 0.95, x);
		col += hue * body * 1.1 * fl + hot * exp(-pow(q.y / 0.06, 2.0)) * smoothstep(0.4, 1.0, x) * smoothstep(1.1, 0.95, x) * 1.2;
		for (int k = 0; k < 24; k++) {
			float fk = float(k);
			float ph = fract(h1(fk + 90.0) + age / 12.0);
			vec2 a = vec2(hw * (0.9 - 2.2 * ph), (h1(fk + 91.0) - 0.5) * 0.8 - 0.2 * ph * ph);
			col += mix(hot, hue, ph) * streak(q, a - vec2(0.12, 0.0), a, 0.012) * (1.0 - ph) * 1.6;
		}
	} else if (shape == 8 || shape == 10) {
		// WATER (ring = shell spin, grab = whirlpool): a cool sheen, spray flung off, droplets that arc and fall
		bool spin = shape == 8;
		vec2 e = spin ? vec2(q.x / max(hw * 0.92, 0.01), q.y / 0.44) : q / 0.46;
		float er = length(e);
		float sheen = exp(-pow((er - 0.9) / 0.18, 2.0)) * (0.7 + 0.3 * sin(atan(e.y, e.x) * 5.0 - age * 0.4));
		if (!spin) sheen = exp(-pow(er / 0.8, 2.0)) * (0.6 + 0.4 * sin(atan(q.y, q.x) * 3.0 + length(q) * 14.0 - age * 0.45));
		col += hue * sheen * 0.55 * fade + hot * pop * blob(q, 0.3) * 0.5;
		// ripple rings
		for (int k = 0; k < 2; k++) {
			float ph = fract(age / 22.0 + float(k) * 0.5);
			float R = (spin ? 1.0 : 0.25) + ph * 0.9;
			vec2 eq = vec2(q.x / (spin ? max(hw, 0.3) : 1.0), (q.y + (spin ? 0.0 : 0.15)) / 0.5);
			col += hue * exp(-pow((length(eq) - R) / 0.04, 2.0)) * (1.0 - ph) * 0.6 * fade;
		}
		for (int k = 0; k < 40; k++) {
			float fk = float(k);
			float ph = fract(h1(fk + 100.0) + age / (16.0 + 8.0 * h1(fk + 101.0)));
			float a = fk * 2.399 + (spin ? age * 0.25 : -age * 0.15);
			vec2 start = spin ? vec2(cos(a) * hw * 0.85, sin(a) * 0.4) : vec2(cos(a), sin(a)) * 0.45 * (1.0 - ph);
			vec2 vel = spin ? vec2(-sin(a), cos(a)) * 1.1 + vec2(cos(a), sin(a)) * 0.5 : vec2(0.0, 0.0);
			if (!spin && h1(fk + 104.0) > 0.55) { start = vec2((h1(fk + 105.0) - 0.5) * 0.4, -0.2); vel = vec2((h1(fk + 106.0) - 0.5) * 1.4, 1.5 + h1(fk + 107.0)); }
			float tt = ph * 0.55;
			vec2 p = start + vel * tt + vec2(0.0, -2.2 * tt * tt);
			vec2 v = vel + vec2(0.0, -4.4 * tt);
			float s = 0.016 + 0.014 * h1(fk + 102.0);
			vec2 r = q - p; vec2 vd = normalize(v + vec2(1e-4)); vec2 n = vec2(-vd.y, vd.x);
			float drop = exp(-pow(dot(r, vd) / (s * 2.2), 2.0) - pow(dot(r, n) / s, 2.0));
			col += mix(hot, hue, 0.6) * drop * 1.3 * (1.0 - ph * 0.6) * fade;
		}
	} else {
		// AFTERIMAGE (fox mirage): a cold ghost-fire glow over the body and wisps rising off it
		float body = exp(-pow(q.x / 0.22, 2.0)) * smoothstep(-0.55, -0.3, q.y) * smoothstep(0.55, 0.35, q.y);
		col += hue * body * 0.7 * fl * fade;
		for (int k = 0; k < 20; k++) {
			float fk = float(k);
			float ph = fract(h1(fk + 120.0) + age / 26.0);
			vec2 p = vec2((h1(fk + 121.0) - 0.5) * 0.4 + 0.06 * sin(ph * 8.0 + fk), -0.45 + ph * 1.1);
			col += mix(hot, hue, ph) * blob(q - p, 0.025 * (1.0 - ph * 0.5)) * (1.0 - ph) * 1.4 * fade;
		}
	}
	// soft edges: nothing reaches the quad border
	vec2 bu = abs(UV - 0.5) * 2.0;
	float edge = (1.0 - smoothstep(0.75, 1.0, bu.x)) * (1.0 - smoothstep(0.75, 1.0, bu.y));
	ALBEDO = col * energy * opacity * edge;
	ALPHA = 1.0;
}
