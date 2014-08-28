/*
    Copyright (C) 2007-2010 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2.  If not, see <http://www.gnu.org/licenses/>.
*/

#pragma once

#ifndef __AFHS_DECRYPTION_CONTEXT_DEFINED
#define __AFHS_DECRYPTION_CONTEXT_DEFINED

#include "AfhsSegmentFragmentCollection.h"
#include "AfhsCurlInstance.h"
#include "Flags.h"
#include "ParameterCollection.h"

#define AFHS_DECRYPTION_CONTEXT_FLAG_NONE                             FLAGS_NONE

#define AFHS_DECRYPTION_CONTEXT_FLAG_LAST                             (FLAGS_LAST + 0)

class CAfhsDecryptionContext : public CFlags
{
public:
  CAfhsDecryptionContext(HRESULT *result);
  ~CAfhsDecryptionContext(void);

  /* get methods */

  // gets AFHS CURL instance (it's only reference to AFHS CURL instance in AFHS protocol)
  // @return : AFHS CURL instance
  CAfhsCurlInstance *GetCurlInstance(void);

  // gets segments and fragments collection (it's only reference to segment fragments in AFHS protocol)
  // @return : segments and fragments collection
  CAfhsSegmentFragmentCollection *GetSegmentsFragments(void);

  // gets which segment and fragment is currently downloading (UINT_MAX means none)
  // @return : which segment and fragment is currently downloading (UINT_MAX means none)
  unsigned int GetSegmentFragmentDownloading(void);

  // gets which segment and fragment is currently processed
  // @return : which segment and fragment is currently processed
  unsigned int GetSegmentFragmentProcessing(void);

  // gets which segment and fragment have to be downloaded
  // @return : which segment and fragment have to be downloaded (UINT_MAX means next segment fragment, always reset after started download of segment and fragment)
  unsigned int GetSegmentFragmentToDownload(void);

  // gets manifest url
  // @return : manifest url
  const wchar_t *GetManifestUrl(void);

  // gets actual configuration (only reference)
  // @return : actual configuration
  CParameterCollection *GetConfiguration(void);

  /* set methods */

  // sets AFHS CURL instance (it's only reference to AFHS CURL instance in AFHS protocol)
  // @param curlInstance : AFHS CURL instance to set
  void SetCurlInstance(CAfhsCurlInstance *curlInstance);

  // sets segments and fragments collection (it's only reference to segment fragments in AFHS protocol)
  // @param segmentFragments : segments and fragments collection to set
  void SetSegmentsFragments(CAfhsSegmentFragmentCollection *segmentFragments);

  // sets which segment and fragment is currently downloading (UINT_MAX means none)
  // @param segmentFragmentDownloading : which segment and fragment is currently downloading (UINT_MAX means none)
  void SetSegmentFragmentDownloading(unsigned int segmentFragmentDownloading);

  // sets which segment and fragment is currently processed
  // @param segmentFragmentProcessing : which segment and fragment is currently processed
  void SetSegmentFragmentProcessing(unsigned int segmentFragmentProcessing);

  // sets which segment and fragment have to be downloaded
  // @param segmentFragmentToDownload : which segment and fragment have to be downloaded (UINT_MAX means next segment fragment, always reset after started download of segment and fragment)
  void SetSegmentFragmentToDownload(unsigned int segmentFragmentToDownload);

  // sets manifest url
  // @param manifestUrl : manifest url to set (only reference, not deep clone)
  void SetManifestUrl(const wchar_t *manifestUrl);

  // sets actual configuration (only reference)
  // @param configuration : actual configuration
  void SetConfiguration(CParameterCollection *configuration);

  /* other methods */

  /* old implementation */

//  /* get methods */
//
//  // gets segments and fragments collection
//  // @return : segments and fragments collection
//  CSegmentFragmentCollection *GetSegmentsFragments(void);
//
//  // gets which segment and fragment is currently downloading (UINT_MAX means none)
//  // @return : which segment and fragment is currently downloading (UINT_MAX means none)
//  unsigned int GetSegmentFragmentDownloading(void);
//
//  // gets which segment and fragment is currently processed
//  // @return : which segment and fragment is currently processed
//  unsigned int GetSegmentFragmentProcessing(void);
//
//  // gets which segment and fragment have to be downloaded
//  // @return : which segment and fragment have to be downloaded (UINT_MAX means next segment fragment, always reset after started download of segment and fragment)
//  unsigned int GetSegmentFragmentToDownload(void);
//
//  // gets manifest url
//  // @return : manifest url
//  const wchar_t *GetManifestUrl(void);
//
//  // gets manifest content
//  // @return : manifest content
//  const wchar_t *GetManifestContent(void);
//
//  // gets force download (download continues even not processed segments and fragments are still there)
//  // return : true if force download, false otherwise
//  bool GetForceDownload(void);
//
//  /* set methods */
//
//  // sets segments and fragments collection
//  // @param segmentsFragments : segments and fragments collection to set (only reference, not deep clone)
//  void SetSegmentsFragments(CSegmentFragmentCollection *segmentsFragments);
//
//  // sets which segment and fragment is currently downloading (UINT_MAX means none)
//  // @param segmentFragmentDownloading : which segment and fragment is currently downloading (UINT_MAX means none)
//  void SetSegmentFragmentDownloading(unsigned int segmentFragmentDownloading);
//
//  // sets which segment and fragment is currently processed
//  // @param segmentFragmentProcessing : which segment and fragment is currently processed
//  void SetSegmentFragmentProcessing(unsigned int segmentFragmentProcessing);
//
//  // sets which segment and fragment have to be downloaded
//  // @param segmentFragmentToDownload : which segment and fragment have to be downloaded (UINT_MAX means next segment fragment, always reset after started download of segment and fragment)
//  void SetSegmentFragmentToDownload(unsigned int segmentFragmentToDownload);
//
//  // sets manifest url
//  // @param manifestUrl : manifest url to set (only reference, not deep clone)
//  void SetManifestUrl(const wchar_t *manifestUrl);
//
//  // sets manifest content
//  // @param manifestContent : manifest content to set (only reference, not deep clone)
//  void SetManifestContent(const wchar_t *manifestContent);
//
//  // sets force download (download continues even not processed segments and fragments are still there)
//  // @param forceDownload : force download to set
//  void SetForceDownload(bool forceDownload);
//
//  /* other methods */
//
protected:
  // holds AFHS CURL instance (it's only reference to AFHS CURL instance in AFHS protocol)
  CAfhsCurlInstance *curlInstance;
  // holds segments and fragments collection (it's only reference to segment fragments in AFHS protocol)
  CAfhsSegmentFragmentCollection *segmentFragments;
  // holds which segment and fragment is currently downloading (UINT_MAX means none)
  unsigned int segmentFragmentDownloading;
  // holds which segment and fragment is currently processed
  unsigned int segmentFragmentProcessing;
  // holds which segment and fragment have to be downloaded
  unsigned int segmentFragmentToDownload;
  // holds manifest url (only reference, not deep clone)
  const wchar_t *manifestUrl;
  // holds actual configuration (only reference)
  CParameterCollection *configuration;

  /* methods */

  /* old implementation */

//  // holds segments and fragments collection
//  // just reference to collection in AFHS protocol
//  CSegmentFragmentCollection *segmentsFragments;
//  // holds which segment and fragment is currently downloading (UINT_MAX means none)
//  unsigned int segmentFragmentDownloading;
//  // holds which segment and fragment is currently processed
//  unsigned int segmentFragmentProcessing;
//  // holds which segment and fragment have to be downloaded
//  // (UINT_MAX means next segment fragment, always reset after started download of segment and fragment)
//  unsigned int segmentFragmentToDownload;
//  // holds manifest url
//  const wchar_t *manifestUrl;
//  // holds manifest content
//  const wchar_t *manifestContent;
//  // specifies if download of requested segment and fragment (segmentFragmentToDownload) have to be forced
//  bool forceDownload;
};

#endif