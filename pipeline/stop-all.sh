#!/bin/bash

# Stops any services running on the dev ports (5000, 5001, 5002).
# Can be called standalone or from other pipeline scripts.
# Works on both Unix (lsof) and Windows/MINGW (netstat + taskkill).

PORTS=(5000 5001 5002)
FOUND=0

kill_on_port() {
  local port=$1

  if command -v lsof &>/dev/null; then
    local pid
    pid=$(lsof -ti :"$port" 2>/dev/null || true)
    if [ -n "$pid" ]; then
      echo "Killing process $pid on port $port"
      kill "$pid" 2>/dev/null || true
      FOUND=1
    fi
  else
    # Windows/MINGW: use netstat to find the PID
    local pid
    pid=$(netstat -ano 2>/dev/null | grep ":${port} " | grep "LISTENING" | awk '{print $NF}' | head -1)
    if [ -n "$pid" ] && [ "$pid" != "0" ]; then
      echo "Killing process $pid on port $port"
      taskkill //F //PID "$pid" > /dev/null 2>&1 || true
      FOUND=1
    fi
  fi
}

for port in "${PORTS[@]}"; do
  kill_on_port "$port"
done

if [ "$FOUND" -eq 0 ]; then
  echo "No services found on ports ${PORTS[*]}"
fi
