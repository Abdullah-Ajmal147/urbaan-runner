# Urban Runner icon layers as SVG (viewBox 0..108, Android adaptive-icon units; safe zone = central 66).
BG = '''
<defs>
 <linearGradient id="sky" x1="0" y1="0" x2="0" y2="1">
  <stop offset="0" stop-color="#22195A"/><stop offset="0.55" stop-color="#151B45"/><stop offset="1" stop-color="#0B0F29"/></linearGradient>
 <radialGradient id="halo" cx="0.5" cy="0.46" r="0.5">
  <stop offset="0" stop-color="#FFB547" stop-opacity="0.55"/><stop offset="0.45" stop-color="#FF3DA6" stop-opacity="0.18"/><stop offset="1" stop-color="#FF3DA6" stop-opacity="0"/></radialGradient>
</defs>
<rect width="108" height="108" fill="url(#sky)"/>
<rect width="108" height="108" fill="url(#halo)"/>
<g fill="#F4F6FF" opacity="0.8">
 <circle cx="18" cy="20" r="0.9"/><circle cx="86" cy="16" r="0.8"/><circle cx="74" cy="30" r="0.6"/><circle cx="30" cy="12" r="0.6"/><circle cx="94" cy="40" r="0.7"/></g>
<g fill="#0E1230">
 <path d="M0 80 h10 v-14 h8 v8 h6 v-18 h9 v24 h6 v-10 h7 v10 h24 v-16 h8 v-10 h7 v26 h6 v-20 h10 v40 H0z"/></g>
<g fill="#2EE6FF" opacity="0.55">
 <rect x="27" y="62" width="2" height="2"/><rect x="27" y="67" width="2" height="2"/><rect x="83" y="64" width="2" height="2"/><rect x="73" y="70" width="2" height="2"/><rect x="12" y="70" width="2" height="2"/></g>
<rect y="88" width="108" height="20" fill="#0B0F29"/>
<rect y="88" width="108" height="1.2" fill="#FF3DA6" opacity="0.7"/>
'''
FG = '''
<defs>
 <linearGradient id="body" x1="0" y1="0" x2="0" y2="1">
  <stop offset="0" stop-color="#FFE3A3"/><stop offset="0.5" stop-color="#FFB547"/><stop offset="1" stop-color="#F07A1A"/></linearGradient>
 <linearGradient id="trailM" x1="1" y1="0" x2="0" y2="0">
  <stop offset="0" stop-color="#FF3DA6"/><stop offset="1" stop-color="#FF3DA6" stop-opacity="0"/></linearGradient>
 <linearGradient id="trailC" x1="1" y1="0" x2="0" y2="0">
  <stop offset="0" stop-color="#2EE6FF"/><stop offset="1" stop-color="#2EE6FF" stop-opacity="0"/></linearGradient>
 <filter id="glow" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="2.2"/></filter>
</defs>
<!-- speed trails -->
<path d="M24 52 L48 50 L48 55 L24 54z" fill="url(#trailM)"/>
<path d="M28 60 L48 58.5 L48 63 L28 61.5z" fill="url(#trailC)"/>
<path d="M32 44 L48 43 L48 46.5 L32 45.5z" fill="url(#trailC)" opacity="0.7"/>
<!-- glow -->
<ellipse cx="60" cy="53" rx="15" ry="17" fill="#FFB547" filter="url(#glow)" opacity="0.9"/>
<!-- hanger + cap -->
<!-- lantern, leaning forward like it's running -->
<g transform="rotate(10 60 53)">
 <path d="M60 26 v6" stroke="#F4F6FF" stroke-width="1.6" stroke-linecap="round"/>
 <rect x="52" y="31" width="16" height="5" rx="2" fill="#2B1700"/>
 <path d="M60 35 C73 35 77 45 77 53 C77 61 73 71 60 71 C47 71 43 61 43 53 C43 45 47 35 60 35z" fill="url(#body)"/>
 <path d="M60 36 C66 40 68 47 68 53 C68 59 66 66 60 71 M60 36 C54 40 52 47 52 53 C52 59 54 66 60 71" fill="none" stroke="#B5196F" stroke-width="1.3" opacity="0.55"/>
 <path d="M50 42 C47 47 47 52 48 57" stroke="#FFF6DD" stroke-width="2.2" stroke-linecap="round" fill="none" opacity="0.85"/>
 <rect x="53" y="70" width="14" height="4.5" rx="2" fill="#2B1700"/>
 <path d="M60 74.5 v6" stroke="#FF3DA6" stroke-width="2" stroke-linecap="round"/>
</g>
'''
import sys
def svg(inner, px, rounded=False):
    clip = '<clipPath id="r"><rect width="108" height="108" rx="24"/></clipPath>' if rounded else ''
    g = f'<g clip-path="url(#r)">{inner}</g>' if rounded else inner
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{px}" height="{px}" viewBox="0 0 108 108"><defs>{clip}</defs>{g}</svg>'
# full art = both layers, scaled so foreground fills more of a flat square icon
FULL = BG + '<g transform="translate(54 54) scale(1.25) translate(-54 -56)">' + FG + '</g>'
for name, inner, rounded in [("bg", BG, False), ("fg", FG, False), ("full", FULL, False), ("round", FULL, True)]:
    open(f"{name}.svg", "w").write(svg(inner, 1024, rounded))
