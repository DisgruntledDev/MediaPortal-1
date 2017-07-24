/* 
 *  Copyright (C) 2005 Team MediaPortal
 *  http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
#include <afx.h>
#include <afxwin.h>

#include <winsock2.h>
#include <ws2tcpip.h>
#include <streams.h>
#include <math.h>
#include "TsFileSeek.h"
#include "..\..\shared\adaptionfield.h"

// For more details for memory leak detection see the alloctracing.h header
#include "..\..\alloctracing.h"

//~65ms of data @ 8Mbit/s
#define SEEK_READ_SIZE (65536)

const double SEEKING_ACCURACY = (double)0.24; // 1/25 *6 (6 frames in PAL)
const int MAX_SEEKING_ITERATIONS = 30;
const int MAX_BUFFER_ITERATIONS = 100;

extern void LogDebug(const char *fmt, ...) ;
CTsFileSeek::CTsFileSeek( CTsDuration& duration)
:m_duration(duration)
{
  m_pFileReadBuffer = NULL;
  m_pFileReadBuffer = new byte[SEEK_READ_SIZE];
}

CTsFileSeek::~CTsFileSeek(void)
{
  if (m_pFileReadBuffer)
  {
    delete [] m_pFileReadBuffer;
    m_pFileReadBuffer = NULL;
  }
  else
  {
    LogDebug("CTsFileSeek::dtor - ERROR m_pFileReadBuffer is NULL !!");
  }
}

void CTsFileSeek::SetFileReader(FileReader* reader)
{
  m_reader=reader;
}

//*********************************************************
// Seeks in the file to the specific timestamp
// refTime : timestamp. Should be 0 < timestamp < duration
//
// The method will make a guess where the timestamp is located in the file
// and do a PCR seek from there until it finds the correct timestamp
//
bool CTsFileSeek::Seek(CRefTime refTime)
{
  if (!m_pFileReadBuffer)
  {
    LogDebug("CTsFileSeek::Seek() - ERROR no buffer !!");
    return true; //EOF
  }

  __int64 fileSize=m_reader->GetFileSize(); 
  //sanity check...
  if (fileSize < 0)
  {
    return true; //Error
  }
  
  double seekTimeStamp = (double)refTime.Millisecs();
  //sanity check  
  if (seekTimeStamp < 50.0) //Less than 50ms from beginning of file
  {
    //simply set the pointer at the beginning of the file
    LogDebug("FileSeek: stop seek at start-of-file, target time: %f", (float)seekTimeStamp);
    m_reader->SetFilePointer(0,FILE_BEGIN);
    return false;
  }
  
  double fileDuration=(double)m_duration.Duration().Millisecs();
  //sanity check
  if (fileDuration <= 0.0)
  {
    return true; //Error
  }


  double bytesPerMS = (double)fileSize/fileDuration;
  double mSfromEnd = fileDuration - seekTimeStamp;
  __int64 offsetBytesFromEnd; 
       
  bool jumpToEnd = false;
  if (m_reader->GetTimeshift())
  {
    offsetBytesFromEnd = (__int64)(-300.0 * bytesPerMS); //0.3s
    if (mSfromEnd < 300.0) jumpToEnd = true; //0.5s
  }
  else
  {
    offsetBytesFromEnd = (__int64)(-3000.0 * bytesPerMS); //3s 
    if (mSfromEnd < 3000.0) jumpToEnd = true; //3s
  }

  //make a guess where should start looking in the file
  double percent=seekTimeStamp/fileDuration;
  __int64 filePos=(__int64)((double)fileSize * percent);

  filePos/=188;
  filePos*=188;
      
  seekTimeStamp /= 1000.0f; // convert to seconds.

  m_seekPid=m_duration.GetPid();
  LogDebug("FileSeek: seek to:%f fileDur:%f filepos:%I64d pid:%x isEnd:%d tShift:%d BPS:%f", 
      (float)seekTimeStamp, (float)(fileDuration/1000.0f), filePos, m_seekPid, jumpToEnd, m_reader->GetTimeshift(), (float)(bytesPerMS*1000.0));
  
  __int64 binaryMax=fileSize;
  __int64 binaryMin=0;
  __int64 lastFilePos=0;
  int seekingIteration=0;
  __int64 firstFilePos=filePos;
  int noPCRIteration=0;
  bool noPCRloop = false;

  Reset() ;   // Reset "PacketSync"
  while (true)
  {
    //sanity checks
    if ((filePos<=0) || (seekTimeStamp < 0.05)) //Less than 50ms from beginning of file
    {
      //no need to seek for timestamp 0,
      //simply set the pointer at the beginning of the file
      LogDebug("FileSeek: stop seek at start-of-file, target time: %f", (float)seekTimeStamp);
      m_reader->SetFilePointer(0,FILE_BEGIN);
      return false;
    }
    
    if ((filePos+SEEK_READ_SIZE > fileSize) || jumpToEnd)
    {
      //No need to seek when we want to seek to end of file.
      //Simply set the pointer at the end of the file minus some time 
      //(to avoid end-of-file triggering annoyances).
      //offsetBytesFromEnd is a negative value
      LogDebug("FileSeek: stop seek at end-of-file: offsetBytesFromEnd %I64d, fileSize: %I64d", offsetBytesFromEnd, fileSize);
      if ((fileSize + offsetBytesFromEnd) <= 0) //File shorter than offset
      {
        m_reader->SetFilePointer(0,FILE_BEGIN);
      }
      else
      {
        m_reader->SetFilePointer(offsetBytesFromEnd, FILE_END);
      }
      return false;
    }

    //set filepointer to filePos
    m_reader->SetFilePointer(filePos,FILE_BEGIN);

    //read buffer from file at position filePos
    DWORD dwBytesRead;
    if (!SUCCEEDED(m_reader->Read(m_pFileReadBuffer, SEEK_READ_SIZE,&dwBytesRead)))
    {
      LogDebug("FileSeek: read failed at filePos: %I64d - target time: %f, iterations: %d", filePos, seekTimeStamp, seekingIteration);
      return true;
    }
    if (dwBytesRead <= 0) //end-of-file
    {
      LogDebug("FileSeek: end-of-file at filePos: %I64d - target time: %f, iterations: %d", filePos, seekTimeStamp, seekingIteration);
      return true;
    }
    //process data
    m_pcrFound.Reset();
    OnRawData2(m_pFileReadBuffer,dwBytesRead);

    //did we find a pcr?
    if (m_pcrFound.IsValid)
    {
      //yes. pcr found
      double clockFound=m_pcrFound.ToClock();
      double diff = clockFound - seekTimeStamp;
      //LogDebug(" got %f at filepos %I64d diff %f ( %I64d, %I64d )", clockFound, filePos, diff, binaryMin, binaryMax);

      // Make sure that seeking position is at least the target one
      if (0 <= diff && diff <= SEEKING_ACCURACY)
      {
        LogDebug("FileSeek: stop seek: %f at %I64d - target: %f, diff: %f, iterations: %d",
          clockFound, filePos, seekTimeStamp, diff, seekingIteration);
        m_reader->SetFilePointer(filePos,FILE_BEGIN);
        return false;
      }

      noPCRIteration = 0;
      seekingIteration++;
      if( seekingIteration > MAX_SEEKING_ITERATIONS )
      {
        LogDebug("FileSeek: stop seek max iterations reached (%d): %f at %I64d - target: %f, diff: %f",
          MAX_SEEKING_ITERATIONS, clockFound, filePos, seekTimeStamp, diff);
          
        if (fabs(diff) < 2.0)
        {
          m_reader->SetFilePointer(filePos,FILE_BEGIN);
        }
        else
        {
          //Set the file pointer to the initial estimate - the best we can do...
          m_reader->SetFilePointer(firstFilePos,FILE_BEGIN);
        }
        return false;
      }

      // lower bound becomes valid
      if( clockFound > seekTimeStamp )
      {
        if (filePos < binaryMax) binaryMax = filePos-1;
      }
      else
      {
        if (filePos > binaryMin) binaryMin = filePos+1;
      }

      lastFilePos=filePos;
      filePos = binaryMin + ( binaryMax - binaryMin ) / 2;
      filePos/=188;
      filePos*=188;

      if (lastFilePos==filePos)
      {
        LogDebug("FileSeek: stop seek closer target found : %f at %I64d - target: %f, diff: %f",
          clockFound, filePos, seekTimeStamp, diff);
        m_reader->SetFilePointer(filePos,FILE_BEGIN);
        return false;
      }

      Reset() ;   // Random jump, Reset "PacketSync"
    }
    else // no first PCR
    {
      //move filepointer forward and continue searching for a PCR
      filePos += SEEK_READ_SIZE;
      noPCRIteration++;
      if (noPCRIteration > MAX_BUFFER_ITERATIONS)
      {
        if (noPCRloop) //second time this has happened
        {
          LogDebug("FileSeek: stop seek, no PCR found, max iterations reached (%d)", MAX_BUFFER_ITERATIONS);
          //Set the file pointer to the initial estimate - the best we can do...
          m_reader->SetFilePointer(firstFilePos,FILE_BEGIN);
          return false;
        }
        //Let's try looking for any PCR pid 
        //starting again from the initial position
        LogDebug("FileSeek: No PCR found (pid = %d), trying for any PCR pid", m_seekPid);
        m_seekPid = -1;
        filePos = firstFilePos;
        noPCRIteration = 0;
        noPCRloop = true;
      }
    }
  }
  return false;
}


//*********************************************************
// Callback method. This method gets called via
// CTsFileSeek::Seek()->OnRawData2(m_pFileReadBuffer,dwBytesRead)
// tsPacket : pointer to 188 byte Transport Stream packet
//
// This method checks if the ts packet contains a PCR timestamp
// and ifso sets the PCR timestamp in m_pcrFound;
void CTsFileSeek::OnTsPacket(byte* tsPacket, int bufferOffset, int bufferLength)
{
  if (m_pcrFound.IsValid) return ;

  CTsHeader header(tsPacket);
  CAdaptionField field;
  field.Decode(header,tsPacket);
  if (field.Pcr.IsValid)
  {
    //got a pcr, is it the correct pid?
    if ( (m_seekPid>0 && (header.Pid==m_seekPid)) || (m_seekPid<0) )
    {
      // pid is valid
      // did we have a pcr rollover ??
      if (m_duration.FirstStartPcr() > m_duration.EndPcr())
      {
        //pcr rollover occured.
        //next we need to convert the pcr into filestamp
        //since we are seeking from 0-duration
        //but the file can start with any pcr timestamp
        if (field.Pcr.ToClock() <=m_duration.EndPcr().ToClock())
        {
          // pcr < endpcr (second half of the file)
          //   pcrFound= pcr+(MAXIMUM_PCR - startpcr)
          // StartPcr------>(0x1ffffffff;0x1ff), (0x0,0x0)--------->EndPcr
          m_pcrFound=field.Pcr;
          double d1=m_pcrFound.ToClock();
          CPcr pcr2;
          pcr2.PcrReferenceBase = 0x1ffffffffULL;
          pcr2.PcrReferenceExtension = 0x1ffULL;
          double start=pcr2.ToClock()- m_duration.StartPcr().ToClock();
          d1+=start;
          m_pcrFound.FromClock(d1);
        }
        else
        {
          //PCR > endpcr (first half of the file)
          //   pcrFound= (pcr-startpcr)
          m_pcrFound=field.Pcr;
          double d1=m_pcrFound.ToClock();
          double start=m_duration.StartPcr().ToClock();//earliest pcr available in the file
          LogDebug(" found clock %f earliest is %f", d1, start);
          d1-=start;
          LogDebug(" after sub %f", d1);
          m_pcrFound.FromClock(d1);
        }
      }
      else
      {
        //no pcr rollover occured.
        //next we need to convert the pcr into filestamp
        //since we are seeking from 0-duration
        //but the file can start with any pcr timestamp
        // formula: pcrfound = pcr-startpcr;
        m_pcrFound=field.Pcr;
        double d1=m_pcrFound.ToClock();
        double start=m_duration.StartPcr().ToClock(); //earliest pcr available in the file
        d1-=start;
        m_pcrFound.FromClock(d1);
      }
    }
  }
}