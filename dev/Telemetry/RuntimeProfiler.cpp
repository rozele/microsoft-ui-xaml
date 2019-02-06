// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

#include <pch.h>
#include "RuntimeProfiler.h"
#include "TraceLogging.h"

#define DEFINE_PROFILEGROUP(name, group, size) \
    CMethodProfileGroup<size>        name(group)

namespace RuntimeProfiler {

    void UninitializeRuntimeProfiler();

    struct FunctionTelemetryCount
    {
        volatile LONG      *pInstanceCount{ nullptr };
        volatile UINT16     uTypeIndex;
        volatile UINT16     uMethodIndex;
    };

    class CMethodProfileGroupBase
    {
    public:
        virtual void RegisterMethod(UINT16 uTypeIndex, UINT16 uMethodIndex, volatile LONG *pCount) noexcept = 0;
        virtual void FireEvent(bool bSuspend) noexcept = 0;
    };

    template <size_t size>
    class CMethodProfileGroup: public CMethodProfileGroupBase
    {
    public:
        static const int TableSize = size;
        
        CMethodProfileGroup(ProfileGroup group)
        :   m_cMethods(0)
        ,   m_group(group)
        {
            //  Note: We are declaring these objects in a global scope which
            //      basically means that anything we call here in the
            //      constructor needs to be safe to call during DllMain.
        }
        
        ~CMethodProfileGroup()
        {
            //  Ditto from above.
            UninitializeRuntimeProfiler();
        }
        
        void RegisterMethod(UINT16 uTypeIndex, UINT16 uMethodIndex, volatile LONG *pCount) noexcept
        {
            static_assert(sizeof(LONG) == sizeof(UINT32), "Since we're using InterlockedIncrement, make sure that this is the same size independent of build flavors.");
            
            //  Zero-based index
            LONG WriteIndex = ::InterlockedIncrement(&m_cMethods) - 1;
            
            if (WriteIndex < (LONG)(m_Counts.max_size()))
            {
                m_Counts[WriteIndex].uTypeIndex          = uTypeIndex;
                m_Counts[WriteIndex].uMethodIndex        = uMethodIndex;
                
                //  Note:  This pointer is the last thing to be set, this is
                //    intentional, FireEvent will check the this pointer and
                //    if set will assume that the rest of this structure is
                //    valid, do not change the order.
                m_Counts[WriteIndex].pInstanceCount      = pCount;
                
                //  FireEvent() will reset counts to zero and we don't want
                //  RegisterMethod() to be called again, thus we set the
                //  initial static value to -1, to be incremented to 0 on first
                //  call and we increment again for an accurate count.
                ::InterlockedIncrement(pCount);
            }
        }
        
        //  This function returns the start of the string presentation of
        //  given number, it assumes the buffer passed has a sufficiently
        //  large buffer to accomodate the number.
        static PWSTR ConstructNumber(_In_ UINT uNumber, _Out_ PWSTR pszEnd)
        {
            *pszEnd = 0;  // null terminate!

            //  Special case 0
            if (0 == uNumber)
            {
                pszEnd--;
                *pszEnd = '0';
            }
            else
            {
                for (; uNumber; uNumber /= 10)
                {
                    pszEnd--;
                    *pszEnd = '0' + (uNumber % 10);
                }
            }

            return (pszEnd);
        }

        //  This function copies the source string to the destination string
        //  and return the pointer to the null terminator, it never writes past
        //  the given last character.
        static PWSTR WriteString(_Out_ PWSTR pszDst, _In_ PCWSTR pszSrc, _In_ PWSTR pszLastChar)
        {
            for (; *pszSrc; pszSrc++, pszDst++)
            {
                *pszDst = *pszSrc;
                if (pszDst == pszLastChar)
                {
                    break;
                }
            }
            *pszDst = 0;

            return (pszDst);
        }

        //  Thin wrapper around the firing of the event, since we may now fire
        //  multiple events if our output buffer fills.
        static void FireEventRaw(PCWSTR pszAPICounts, UINT32 uGroupId, UINT32 cMethods, bool bSuspend, bool bOverflow, bool bStringOverflow)
        {
            TraceLoggingWrite(
                g_hTelemetryProvider,
                "RuntimeProfiler",
                TraceLoggingDescription("XAML methods that have been called."),
                TraceLoggingWideString(pszAPICounts, "ApiCounts"),
                TraceLoggingUInt32(uGroupId, "ProfileGroupId"),
                TraceLoggingUInt32(cMethods,"TotalCount"),
                TraceLoggingBoolean(bSuspend, "OnSuspend"),
                TraceLoggingBoolean(bOverflow, "Overflow"),
                TraceLoggingBoolean(bStringOverflow, "StringOverflow"),
                TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
                TraceLoggingKeyword(MICROSOFT_KEYWORD_MEASURES));
        }

        //  Note:  This method is now called during process detach, thus we
        //         may not call any API outside of kernel or risk acquiring the
        //         loader lock, so all string formatting is done manually.
        //         The only API's that we call are:
        //            InterlockedExchange()
        //            TraceLoggingWrite()
        void FireEvent(bool bSuspend) noexcept
        {
            if (!g_IsTelemetryProviderEnabled)
            {
                // Trace logging provider for Microsoft telemetry is not enabled. Exit right away.
                return;
            }

            //  Each entry will look like this:
            //  [XX|YYYY]:ZZZ
            //  Conservatively accounting for 20 characters per entry depending
            //  on the length of the numbers.
            WCHAR       OutputBuffer[20 * TableSize];
            WCHAR       Number[15];     //  MAX_INT is 4294967295
            UINT32      cMethods = (UINT32)m_cMethods;
            bool        bStringOverflow = false;

            //  Used to construct output string.
            PWSTR       pszOutput = &(OutputBuffer[0]);
            PWSTR       pszLastEntry = pszOutput;
            PWSTR       pszLastChar = &(OutputBuffer[_countof(OutputBuffer) - 1]);
            PWSTR       pszNumEnd = &(Number[_countof(Number) - 1]);

            for (UINT ii = 0; ii < cMethods; ii++)
            {
                UINT    cHits;

                if (!m_Counts[ii].pInstanceCount)
                {
                    //  In the middle of RegisterMethod on another thread,
                    //  we'll forgo logging this method for now and pick it
                    //  up on the next FireEvent().
                    continue;
                }

                //  Zeroing out AND getting current value.
                cHits = (UINT)(::InterlockedExchange(m_Counts[ii].pInstanceCount, 0));

                if (0 == cHits)
                {
                    //  Next!
                    continue;
                }

                for (;;)
                {
                    PWSTR   pszWrite = pszLastEntry;

                    //  If not first entry, enter a separator...
                    if (pszWrite != pszOutput)
                    {
                        pszWrite = WriteString(pszWrite, L",", pszLastChar);
                    }

                    //  The entry in the list will look like
                    //    '[type index|method index]:count'
                    pszWrite = WriteString(pszWrite, L"[", pszLastChar);
                    pszWrite = WriteString(pszWrite, ConstructNumber((m_Counts[ii].uTypeIndex), pszNumEnd), pszLastChar);
                    pszWrite = WriteString(pszWrite, L"|", pszLastChar);
                    pszWrite = WriteString(pszWrite, ConstructNumber((m_Counts[ii].uMethodIndex), pszNumEnd), pszLastChar);
                    pszWrite = WriteString(pszWrite, L"]:", pszLastChar);
                    pszWrite = WriteString(pszWrite, ConstructNumber(cHits, pszNumEnd), pszLastChar);

                    if (pszWrite == pszLastChar)
                    {
                        //  Our buffer is full...
                        *pszLastEntry = 0;  // truncating to last full entry we could accomodate
                        pszLastEntry = pszOutput;  // resetting the write buffer to process additional events
                        FireEventRaw(pszOutput, m_group, cMethods, bSuspend, false, true);  //  Firing the event

                        //  And loop through to process this entry again.
                    }
                    else
                    {
                        //  Wrote string.  Next!
                        pszLastEntry = pszWrite;
                        break;
                    }
                }
            }

            if (pszLastEntry != pszOutput)
            {
                //  We have something to log in the event.
                FireEventRaw(pszOutput, m_group, cMethods, bSuspend, false, false);
            }
        }
        
    private:
        std::array<FunctionTelemetryCount, TableSize>   m_Counts = {0};
        LONG                                            m_cMethods;
        ProfileGroup                                    m_group;
    };  //  class CMethodProfileGroup


    //  Yes, we're declaring this as a global, the ctor/dtor are implemented
    //  very carefully and this will not create issues with DllMain().
    DEFINE_PROFILEGROUP(gGroupClasses, PG_Class, ProfId_Size);

    struct ProfileGroupInfo
    {
        CMethodProfileGroupBase    *pGroup;
        char                       *pszGroupName;
    } gProfileGroups[] = 
    {
        { static_cast<CMethodProfileGroupBase*>(&gGroupClasses), "Classes" },
    };

    using namespace std::chrono;
    constexpr auto  EventFrequency = 20min;

    void FireEvent(bool bSuspend) noexcept
    {
        for (auto group : gProfileGroups)
        {
            group.pGroup->FireEvent(bSuspend);
        }
    }

    VOID CALLBACK TPTimerCallback(PTP_CALLBACK_INSTANCE, PVOID, PTP_TIMER) noexcept
    {
        FireEvent(false);
    }

    PTP_TIMER   g_pTimer = NULL;

    BOOL CALLBACK CancelTimer(PINIT_ONCE /* InitOnce */, PVOID /* Parameter */, PVOID* /* context */)
    {
        if (NULL != g_pTimer)
        {
            //  Canceling timer.
            
            //  Note:  We're called on a global destructor, so we are not
            //    calling WaitForThreadpoolTimerCallbacks() to prevent
            //    deadlocks.
            SetThreadpoolTimer(g_pTimer, NULL, 0, 0);
            CloseThreadpoolTimer(g_pTimer);
            
            
            g_pTimer = NULL;
        }
        
        //  Either way, no active timer.
        return (TRUE);
    }

    void UninitializeRuntimeProfiler()
    {
        static INIT_ONCE    UninitProfiler = INIT_ONCE_STATIC_INIT;
        
        InitOnceExecuteOnce(&UninitProfiler, CancelTimer, NULL, NULL);
    }

    BOOL CALLBACK InitializeRuntimeProfiler(PINIT_ONCE /* InitOnce */, PVOID /* Parameter */, PVOID* /* context */)
    {
        g_pTimer = ::CreateThreadpoolTimer(TPTimerCallback, nullptr, nullptr);
    
        if (NULL != g_pTimer)
        {
            LARGE_INTEGER   lidueTime;
            FILETIME        ftdueTime;

            //  Setting periodic timer.  Using negative time in 100 nanosecond
            //  intervals to indicate relative time.
            lidueTime.QuadPart = -10000 * (LONGLONG)(std::chrono::milliseconds(EventFrequency).count());
        
            ftdueTime.dwHighDateTime = (DWORD)(lidueTime.HighPart);
            ftdueTime.dwLowDateTime  = lidueTime.LowPart;
            
            //  Setting the callback window length to 60 seconds since the
            //  timing of the event is not critical
            SetThreadpoolTimer(g_pTimer, &ftdueTime, (DWORD)(std::chrono::milliseconds(EventFrequency).count()), 60 * 1000);
        }
        
        //  Since MUX doesn't piggyback the WUX Extension suspend handler,
        //  we sign up for suspension notifications.
#ifndef BUILD_WINDOWS
        winrt::Application::Current().Suspending(([](auto &, auto &)
            {
                FireEvent(true);
            }
        ));
#endif

        return ((NULL != g_pTimer)?TRUE:FALSE);
    }

    void RegisterMethod(ProfileGroup group, UINT16 uTypeIndex, UINT16 uMethodIndex, volatile LONG *pCount) noexcept
    {
        static INIT_ONCE            InitProfiler = INIT_ONCE_STATIC_INIT;
        CMethodProfileGroupBase    *pGroup = gProfileGroups[(int)group].pGroup;
    
        InitOnceExecuteOnce(&InitProfiler, InitializeRuntimeProfiler, NULL, NULL);

        return (pGroup->RegisterMethod(uTypeIndex, uMethodIndex, pCount));
    }

} // namespace RuntimeProfiler

//  This will be exported by WUX Extension library
STDAPI_(void) SendTelemetryOnSuspend() noexcept
{
    RuntimeProfiler::FireEvent(true);
}
