"use client";

import { useEffect, useState } from "react";

// The current time as state, so a screen can compare dates without calling
// Date.now while it renders. It moves forward once a minute, which is all a
// day-level countdown needs.
export function useNow(intervalMs = 60_000) {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), intervalMs);

    return () => clearInterval(timer);
  }, [intervalMs]);

  return now;
}
