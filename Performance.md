# Clustered vs Forward volumetric fog performance

All rendered at 2560x1521 resolution on an NVIDIA GeForce RTX 3050 Laptop GPU

## Three towers - Lanterns All (44 lights) - Clustered

Frame time: 5.74ms

Depth pre pass: 1.45ms
Cluster pass: 0.05ms
Volume pass: 1.02ms
Color pass: 2.64ms

## Three towers - Lanterns All (44 lights) - Forward

Frame time: 10.6ms

Depth pre pass: 1.59ms
Volume pass: 4.31ms
Color pass: 4.09ms

## Three towers - 400 random lights (401 lights) - Clustered

Frame time: 33.15ms

Depth pre pass: 1.85ms
Cluster pass: 0.44ms
Volume pass: 7.9ms
Color pass: 22.3ms

## Three towers - 400 random lights (401 lights) - Forward

Frame time: 72.2ms

Depth pre pass: 1.68ms
Volume pass: 32.72ms
Color pass: 37.16ms

## Pillar - Pillar (7 lights) - Clustered

Frame time: 3.81ms

Depth pre pass: 0.52ms
Cluster pass: 0.03ms
Volume pass: 1.0ms
Color pass: 1.75ms

## Pillar - Pillar (7 lights) - Forward

Frame time: 4.08ms

Depth pre pass: 0.54ms
Volume pass: 1.31ms
Color pass: 1.71ms

## Top of the mountain - Sun (1 light) - Clustered

Frame time: 4.63ms

Depth pre pass: 0.66ms
Cluster pass: 0.02ms
Volume pass: 1.11ms
Color pass: 2.25ms

## Top of the mountain - Sun (1 light) - Forward

Frame time: 4.16ms

Depth pre pass: 0.60ms
Volume pass: 0.96ms
Color pass: 2.04ms

## Tower closeup - Lantern All Sun (45 lights) - Clustered

Frame time: 8.41ms

Depth pre pass: 1.43ms
Cluster pass:
Volume pass: 1.12ms
Color pass: 2.59ms

## Tower closeup - Lantern All Sun (45 lights) - Forward

Frame time: 13.71ms

Depth pre pass: 1.61ms
Volume pass: 4.38ms
Color pass: 4.19ms

# Overhead - 400 random lights (401 lights) - Clustered

Frame time: 46.87ms

Depth pre pass: 0.66ms
Cluster pass: 0.38ms
Volume pass: 2.72ms
Color pass: 42.47ms

# Overhead - 400 random lights (401 lights) - Forward

Frame time: 76.25ms

Depth pre pass: 0.63ms
Volume pass: 32.18ms
Color pass: 42.80ms