// Worker.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include <stdint.h>
#include <math.h>
#include <algorithm>

#define HOST_EXPORT __declspec(dllexport) 

template<typename T>
T clamp(T value, T min_value, T max_value)
{
  return std::min<T>(max_value, std::max<T>(value, min_value));
}

extern "C"
{
  struct BGRA32
  {
    uint8_t b;
    uint8_t g;
    uint8_t r;
    uint8_t a;
  };

  // mapping is [(time, value)]
  struct Mapping
  {
    float time, value;
  };

  float remap(float t, Mapping *mapping, int mapping_count)
  {
    // find where in the mapping t is
    if (t < mapping->time)
      return mapping->value;
    if (t >= mapping[mapping_count-1].time)
      return mapping[mapping_count-1].value;

    int idx = 0;
    bool found = false;
    while (idx < mapping_count-1) {
      if (t >= mapping[idx].time && t < mapping[idx+1].time)
        break;
      idx++;
    }

    // linear interpolate
    Mapping *cur = &mapping[idx];
    Mapping *next = &mapping[idx+1];
    float ofs = (t - cur->time) / (next->time - cur->time);
    return (1-ofs) * cur->value + ofs * next->value;
  }


  HOST_EXPORT void __stdcall create_bitmap(void *ptr, int width, int height, int bpp, float *mapping, int mapping_count)
  {
    if (bpp != 32)
      return;

    const float max_len = sqrtf(1+1);

    BGRA32 *p = (BGRA32 *)ptr;
    for (int y = 0; y < height; ++y) {
      for (int x = 0; x < width; ++x) {
        float dx = 2 * fabsf((float)width/2 - x) / width;
        float dy = 2 * fabsf((float)height/2 - y) / height;
        float dist = sqrtf(dx*dx+dy*dy);
        int c = (int)(255 * remap(1 - clamp(dist, 0.0f, 1.0f), (Mapping *)mapping, mapping_count/2));
        p->r = c;
        p->g = c;
        p->b = c;
        p->a = c;
        p++;
      }
    }
  }

};


