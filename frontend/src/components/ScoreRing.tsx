interface ScoreRingProps {
  score: number;
  size?: number;
  stroke?: number;
  label?: string;
}

function colorFor(score: number) {
  if (score >= 80) return "#66bb6a";
  if (score >= 60) return "#ffb300";
  if (score >= 40) return "#ff9800";
  return "#ef5350";
}

export default function ScoreRing({ score, size = 160, stroke = 12, label }: ScoreRingProps) {
  const r = (size - stroke) / 2;
  const c = 2 * Math.PI * r;
  const clamped = Math.max(0, Math.min(100, score));
  const offset = c - (clamped / 100) * c;
  const color = colorFor(clamped);
  const cx = size / 2;

  return (
    <div className="score-ring" style={{ width: size, height: size }}>
      <svg width={size} height={size}>
        <circle cx={cx} cy={cx} r={r} fill="none" stroke="rgba(255,255,255,0.08)" strokeWidth={stroke} />
        <circle
          cx={cx}
          cy={cx}
          r={r}
          fill="none"
          stroke={color}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={c}
          strokeDashoffset={offset}
          transform={`rotate(-90 ${cx} ${cx})`}
          style={{ transition: "stroke-dashoffset 0.9s ease" }}
        />
      </svg>
      <div className="score-ring-label">
        <span className="score-ring-value" style={{ color }}>
          {Math.round(clamped)}
        </span>
        <span className="score-ring-text">{label ?? "out of 100"}</span>
      </div>
    </div>
  );
}