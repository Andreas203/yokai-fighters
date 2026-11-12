// YOK-242: procedural projectile and special-move effects (Fight/Vfx/VfxLayer.cs). Presentation only.
// Paper-talisman / toon look (V1): flat fills, a two-tone core, an ink outline drawn by dilating the shape.
// Animated ONLY from the sim: `age` is sim ticks since spawn and `life` is 0..1 of the effect's life, both set from
// Match.Tick by the layer, so a replay draws the same frames (F3). No TIME, no noise texture.
// `shape` = VfxLook: 0 orb, 1 ink_wave, 2 fox_flame, 3 seal, 4 ink_splash, 5 flame_burst, 6 dust, 7 trail, 8 ring,
// 9 afterimage, 10 grab. When `use_tex` is set the image replaces the shape (VfxArt: preset texture or default file).
shader_type spatial;
render_mode unshaded, cull_disabled, depth_test_disabled, shadows_disabled, blend_mix;

uniform int shape = 0;
uniform vec4 colour : source_color = vec4(1.0);
uniform vec4 core : source_color = vec4(0.945, 0.910, 0.831, 1.0);
uniform vec4 ink : source_color = vec4(0.114, 0.106, 0.129, 1.0);
uniform float age = 0.0;
uniform float life = 0.0;
uniform float opacity = 1.0;
uniform float flip = 1.0;
uniform float aspect = 1.0;
uniform float rim = 0.045;
uniform bool use_tex = false;
uniform bool tint = true;
uniform sampler2D tex : source_color, filter_linear, repeat_disable;
uniform int columns = 1;
uniform int frame = 0;


float ease_out(float t) { return 1.0 - (1.0 - t) * (1.0 - t); }
float inside(float d) { return 1.0 - smoothstep(-0.006, 0.006, d); } // d < 0 inside, a crisp toon edge
float hash1(float n) { return fract(sin(n * 12.9898) * 43758.5453); }

// Every shape gets q in "height units": y in [-0.5, 0.5] (up), x in [-hw, hw] with +x = the way it travels/faces.
// Returns (fill, core).
vec2 orb(vec2 q) {
	float r = length(q);
	float a = atan(q.y, q.x);
	float wob = 0.025 * sin(age * 0.35 + a * 5.0);
	return vec2(inside(r - 0.40 - wob), inside(r - 0.21 - 0.02 * sin(age * 0.5)));
}

vec2 ink_wave(vec2 q, float hw) {
	// a crescent wave whose front sits on the hitbox's leading edge, brush streaks trailing behind it
	vec2 oc = vec2(hw - 0.48, 0.0);
	float outer = length(q - oc) - 0.47;
	float inner = length(q - oc + vec2(0.24, 0.0)) - 0.50;
	float crescent = inside(max(outer, -inner));
	float streak = 0.0;
	float tailx = (q.x + hw) / max(hw * 2.0 - 0.45, 0.01); // 0 at the tail, 1 near the crest
	for (int k = 0; k < 3; k++) {
		float yk = (float(k) - 1.0) * 0.27 + 0.03 * sin(q.x * 7.0 + age * 0.3 + float(k));
		float thick = 0.07 * clamp(tailx, 0.0, 1.0);
		float dash = step(fract(q.x * 2.2 - age * 0.09 + float(k) * 0.37), 0.72);
		streak = max(streak, inside(abs(q.y - yk) - thick) * dash * step(q.x, hw - 0.3));
	}
	// the ofuda riding in the wave: rice-paper core with ink glyph strokes
	vec2 tp = q - vec2(hw - 0.62, 0.0);
	float paper = inside(max(abs(tp.x) - 0.075, abs(tp.y) - 0.24));
	float glyph = step(abs(tp.x), 0.03) * step(0.5, fract(tp.y * 9.0)) * step(abs(tp.y), 0.18);
	float highlight = inside(abs(outer + 0.06) - 0.022) * crescent;
	float fill = max(max(crescent, streak), paper);
	return vec2(fill, max(paper * (1.0 - glyph), highlight));
}

vec2 fox_flame(vec2 q, float hw) {
	// kitsune-bi: a round head on the leading edge, flickering tongues trailing back
	float hr = 0.34 + 0.02 * sin(age * 0.7);
	vec2 hc = vec2(hw - hr - 0.02, 0.0);
	float head = length(q - hc) - hr;
	float tailEnd = -hw + 0.02;
	float s = clamp((q.x - tailEnd) / max(hc.x - tailEnd, 0.01), 0.0, 1.0);
	float sway = 0.09 * sin(q.x * 6.0 + age * 0.45) * (1.0 - s);
	float halfw = hr * pow(s, 0.85) * (0.88 + 0.12 * sin(q.x * 9.0 - age * 0.6));
	float tail = abs(q.y - sway) - halfw;
	tail = q.x > hc.x ? 1.0 : tail;
	float d = min(head, tail);
	float coreHead = length(q - hc - vec2(0.04, 0.0)) - hr * 0.5;
	float coreTail = q.x > hc.x ? 1.0 : abs(q.y - sway) - halfw * 0.42;
	return vec2(inside(d), inside(min(coreHead, coreTail)));
}

vec2 seal(vec2 q) {
	float t = ease_out(life);
	float r = length(q);
	float a = atan(q.y, q.x);
	float R = mix(0.16, 0.42, t);
	float ring = inside(abs(r - R) - 0.035 * (1.0 - 0.5 * life));
	float ticks = step(abs(fract(a / TAU * 12.0 + life * 0.5) - 0.5), 0.14) * step(R + 0.03, r) * step(r, R + 0.075);
	float pop = min(1.0, life * 6.0);
	vec2 pq = q / max(pop, 0.01);
	float paper = inside(max(abs(pq.x) - 0.08, abs(pq.y) - 0.2));
	float glyph = step(abs(pq.x), 0.03) * step(0.5, fract(pq.y * 8.0)) * step(abs(pq.y), 0.15);
	return vec2(max(max(ring, ticks), paper), paper * (1.0 - glyph));
}

vec2 ink_splash(vec2 q) {
	float t = ease_out(life);
	float fill = inside(length(q) - 0.17 * (1.0 - 0.6 * life));
	float c = inside(length(q) - 0.07 * (1.0 - life));
	for (int k = 0; k < 5; k++) {
		float a = float(k) * TAU / 5.0 + float(k) * 0.4;
		float d = mix(0.08, 0.36, t) * (0.75 + 0.25 * hash1(float(k)));
		vec2 c0 = vec2(cos(a), sin(a)) * d;
		float rr = 0.10 * (1.0 - 0.5 * life) * (0.7 + 0.3 * hash1(float(k) + 3.0));
		fill = max(fill, inside(length(q - c0) - rr));
		c = max(c, inside(length(q - c0) - rr * 0.35));
	}
	return vec2(fill, c);
}

vec2 flame_burst(vec2 q) {
	float t = ease_out(life);
	float r = length(q);
	float a = atan(q.y, q.x);
	float R = (0.16 + 0.26 * t) * (0.6 + 0.4 * pow(abs(cos(a * 3.5 + age * 0.2)), 2.0)) * (1.0 - 0.3 * life);
	return vec2(inside(r - R), inside(r - R * 0.45));
}

vec2 dust(vec2 q, float hw) {
	// pale overlapping clouds (the core colour) rolling out from the pound, the move's colour along the ground
	float t = ease_out(life);
	float fill = 0.0;
	for (int k = 0; k < 7; k++) {
		float side = (float(k) - 3.0) / 3.0;
		float cx = side * hw * 0.78 * (0.3 + 0.7 * t);
		float rk = (0.26 + 0.1 * hash1(float(k) + 7.0)) * (0.5 + 0.5 * t) * (1.0 - 0.3 * life) * (1.0 - 0.3 * abs(side));
		vec2 c0 = vec2(cx, -0.5 + rk * 0.7 + 0.05 * hash1(float(k) + 2.0));
		fill = max(fill, inside(length(q - c0) - rk));
	}
	fill *= step(-0.5, q.y);
	return vec2(fill, fill * step(-0.36, q.y));
}

vec2 trail(vec2 q, float hw) {
	// brush-stroke speed lines: each dash thick at its head, tapering back (paper-talisman ink, V1)
	float x = (q.x + hw) / (2.0 * hw); // 0 far behind, 1 at the fighter
	float fill = 0.0;
	for (int k = 0; k < 5; k++) {
		float yk = -0.34 + float(k) * 0.17 + 0.03 * hash1(float(k) + 4.0);
		float ph = fract(x * 1.8 + age * 0.1 + hash1(float(k)));
		float on = step(ph, 0.7);
		float thick = 0.026 * (ph / 0.7) * (0.3 + 0.7 * x);
		fill = max(fill, inside(abs(q.y - yk) - thick) * on * step(0.08 + 0.25 * hash1(float(k) + 1.0), x));
	}
	return vec2(fill, 0.0);
}

vec2 ring(vec2 q, float hw) {
	vec2 e = vec2(q.x / max(hw * 0.92, 0.01), q.y / 0.44);
	float er = length(e);
	float a = atan(e.y, e.x);
	float seg = step(fract(a / TAU * 6.0 - age * 0.06), 0.66);
	return vec2(inside(abs(er - 0.9) - 0.09) * seg, inside(abs(er - 0.9) - 0.03) * seg);
}

vec2 afterimage(vec2 q) {
	// the body left behind: head, shoulders and a flared robe, sliced by fine scan lines that drift down
	float head = length(q - vec2(0.0, 0.37)) - 0.1;
	float w = q.y > 0.18 ? 0.16 : 0.12 + 0.16 * (0.18 - q.y) / 0.68;
	float body = max(abs(q.x) - w, max(-0.5 - q.y, q.y - 0.26));
	float d = min(head, body);
	float scan = step(fract(q.y * 24.0 + age * 0.2), 0.72);
	return vec2(inside(d) * scan, inside(d + 0.04) * scan);
}

vec2 grab(vec2 q) {
	float r = length(q);
	float a = atan(q.y, q.x);
	float arms = fract(a / TAU * 3.0 + r * 2.4 + age * 0.05);
	float shrink = 1.0 - 0.4 * life;
	float fill = step(abs(arms - 0.5), 0.17) * step(r, 0.46 * shrink) * step(0.05, r);
	return vec2(max(fill, inside(r - 0.12 * shrink)), inside(r - 0.08 * shrink));
}

vec2 shape_at(vec2 q, float hw) {
	if (shape == 0) return orb(q);
	if (shape == 1) return ink_wave(q, hw);
	if (shape == 2) return fox_flame(q, hw);
	if (shape == 3) return seal(q);
	if (shape == 4) return ink_splash(q);
	if (shape == 5) return flame_burst(q);
	if (shape == 6) return dust(q, hw);
	if (shape == 7) return trail(q, hw);
	if (shape == 8) return ring(q, hw);
	if (shape == 9) return afterimage(q);
	return grab(q);
}

void fragment() {
	vec2 uv = UV;
	if (flip < 0.0) uv.x = 1.0 - uv.x;
	if (use_tex) {
		float cols = float(max(columns, 1));
		vec4 t = texture(tex, vec2((uv.x + float(frame)) / cols, uv.y));
		ALBEDO = tint ? t.rgb * colour.rgb : t.rgb;
		ALPHA = t.a * opacity;
	} else {
		float hw = aspect * 0.5;
		vec2 q = vec2((uv.x - 0.5) * aspect, 0.5 - uv.y);
		vec2 s = shape_at(q, hw);
		float outer = s.x;
		for (int k = 0; k < 8; k++) {
			float a = float(k) * TAU / 8.0;
			outer = max(outer, shape_at(q + vec2(cos(a), sin(a)) * rim, hw).x);
		}
		float edge = clamp(outer - s.x, 0.0, 1.0);
		vec3 fill = mix(colour.rgb, core.rgb, s.y);
		ALBEDO = mix(fill, ink.rgb, edge);
		ALPHA = clamp(max(s.x, edge), 0.0, 1.0) * opacity * (shape == 9 ? 0.7 : 1.0); // the afterimage is a ghost
	}
}
