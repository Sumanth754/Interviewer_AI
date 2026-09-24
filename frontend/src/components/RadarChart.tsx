import type { TagStat } from "@/lib/types";

interface RadarChartProps {
  stats: TagStat[];
  size?: number;
}

export default function RadarChart({ stats, size = 320 }: RadarChartProps) {
  const cx = size / 2;
  const cy = size / 2;
  const radius = size * 0.34;
  const tags = stats.map((s) => s.tag);
  const levels = [0.25, 0.5, 0.75, 1];

  const pointAt = (index: number, fraction: number) => {
    const angle = (Math.PI * 2 * index) / tags.length - Math.PI / 2;
    return {
      x: cx + Math.cos(angle) * radius * fraction,
      y: cy + Math.sin(angle) * radius * fraction,
    };
  };

  const polygonFor = (fraction: number) =>
    tags.map((_, i) => {
      const p = pointAt(i, fraction);
      return `${p.x.toFixed(1)},${p.y.toFixed(1)}`;
    });

  const valuePolygon = tags.map((_, i) => {
    const value = Math.max(stats[i]?.average ?? 0, 0) / 100;
    const p = pointAt(i, value);
    return `${p.x.toFixed(1)},${p.y.toFixed(1)}`;
  });

  const labelPositions = tags.map((_, i) => {
    const p = pointAt(i, 1.18);
    return { label: tags[i], x: p.x, y: p.y, value: Math.round(stats[i]?.average ?? 0), level: stats[i]?.level ?? "" };
  });

  return (
    <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img" aria-label="Skill radar chart">
      <defs>
        <linearGradient id="radarFill" x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#7c5cff" stopOpacity="0.55" />
          <stop offset="100%" stopColor="#00b8d4" stopOpacity="0.35" />
        </linearGradient>
      </defs>
      {levels.map((f) => (
        <polygon key={f} points={polygonFor(f).join(" ")} fill="none" stroke="rgba(255,255,255,0.12)" strokeWidth="1" />
      ))}
      {tags.map((_, i) => {
        const a = pointAt(i, 1);
        const b = pointAt(i, 0);
        return <line key={i} x1={a.x} y1={a.y} x2={b.x} y2={b.y} stroke="rgba(255,255,255,0.07)" strokeWidth="1" />;
      })}
      <polygon points={valuePolygon.join(" ")} fill="url(#radarFill)" stroke="#7c5cff" strokeWidth="2" strokeLinejoin="round" />
      {tags.map((_, i) => {
        const value = Math.max(stats[i]?.average ?? 0, 0) / 100;
        const p = pointAt(i, value);
        return <circle key={i} cx={p.x} cy={p.y} r="3.5" fill="#00e5ff" />;
      })}
      {labelPositions.map((l) => (
        <g key={l.label}>
          <text x={l.x} y={l.y} textAnchor="middle" dominantBaseline="middle" fill="rgba(255,255,255,0.85)" fontSize="11" fontWeight="600">
            {l.label}
          </text>
          <text x={l.x} y={l.y + 14} textAnchor="middle" dominantBaseline="middle" fill="#9fb0c9" fontSize="10">
            {l.value}%
          </text>
        </g>
      ))}
    </svg>
  );
}