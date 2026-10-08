#include "StretchVendor/signalsmith-stretch.h"
#include <new>
#ifdef _WIN32
#define CR_EXPORT extern "C" __declspec(dllexport)
#else
#define CR_EXPORT extern "C" __attribute__((visibility("default")))
#endif
namespace {
struct Interleaved {
    float *data; int channels;
    struct Channel {float *data; int stride; float &operator[](int i){return data[i*stride];}};
    Channel operator[](int c){return {data+c,channels};}
};
struct Processor {
    signalsmith::stretch::SignalsmithStretch<float> stretch{1};
    int channels;
    Processor(int ch,int rate):channels(ch){stretch.presetDefault(ch,float(rate));}
};
}
CR_EXPORT void *CRStretchCreate(int channels,int rate){
    if(channels<1||channels>8||rate<8000||rate>192000)return nullptr;
    try{return new Processor(channels,rate);}catch(...){return nullptr;}
}
CR_EXPORT void CRStretchDestroy(void *p){delete static_cast<Processor *>(p);}
CR_EXPORT int CRStretchInputLatency(void *p){return static_cast<Processor *>(p)->stretch.inputLatency();}
CR_EXPORT int CRStretchOutputLatency(void *p){return static_cast<Processor *>(p)->stretch.outputLatency();}
CR_EXPORT int CRStretchSeek(void *p,float *data,int frames,double rate){
    if(!p||!data||frames<0||rate<.25||rate>2)return 0;
    try{auto &v=*static_cast<Processor *>(p);v.stretch.seek(Interleaved{data,v.channels},frames,rate);return 1;}catch(...){return 0;}
}
CR_EXPORT int CRStretchProcess(void *p,float *input,int inFrames,float *output,int outFrames){
    if(!p||!input||!output||inFrames<0||outFrames<1)return 0;
    try{auto &v=*static_cast<Processor *>(p);v.stretch.process(Interleaved{input,v.channels},inFrames,Interleaved{output,v.channels},outFrames);return 1;}catch(...){return 0;}
}
