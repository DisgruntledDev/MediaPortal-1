using System;
using System.Threading;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using System.Collections;
using SQLite.NET;
using Core.Util;
using MediaPortal.Database;
using MediaPortal.TagReader;

using System.IO;

using System.Globalization;




namespace MediaPortal.Music.Database
{

	public delegate void MusicDBReorgEventHandler(object sender, DatabaseReorgEventArgs e);

	
	public class DatabaseReorgEventArgs : System.EventArgs 
	{
		public int progress;
		// Provide one or more constructors, as well as fields and
		// accessors for the arguments.
		public string phase;
	}

  public class MusicDatabase
  {

	  public class CArtistCache
    {
      public int idArtist = 0;
      public string strArtist = String.Empty;
    };

    public class CPathCache
    {
      public int idPath = 0;
      public string strPath = String.Empty;
    };

    public class CGenreCache
    {
      public int idGenre = 0;
      public string strGenre = String.Empty;
    };

    public class AlbumInfoCache : AlbumInfo
    {
      public int idAlbum = 0;
      public int idArtist = 0;
    };


    public class ArtistInfoCache : ArtistInfo
    {
      public int idArtist = 0;
    }

    ArrayList m_artistCache = new ArrayList();
    ArrayList m_genreCache = new ArrayList();
    ArrayList m_pathCache = new ArrayList();
    ArrayList m_albumCache = new ArrayList();

	ArrayList m_pathids = new ArrayList();
	ArrayList m_shares = new ArrayList ();




	// An event that clients can use to be notified whenever the
	// elements of the list change.
	public event MusicDBReorgEventHandler DatabaseReorgChanged;


	// Invoke the Changed event; called whenever list changes
	protected virtual void OnDatabaseReorgChanged(DatabaseReorgEventArgs e) 
	{
		  if (DatabaseReorgChanged != null)
			  DatabaseReorgChanged(this, e);
	}


	enum Errors
	  {
		  ERROR_OK					=	317
		, ERROR_CANCEL				=	0
		, ERROR_DATABASE			=	315
		, ERROR_REORG_SONGS			=	319			
		, ERROR_REORG_ARTIST		=	321
		, ERROR_REORG_GENRE			=	323
		, ERROR_REORG_PATH			=	325
		, ERROR_REORG_ALBUM			=	327
		, ERROR_WRITING_CHANGES		=	329	
		, ERROR_COMPRESSING			=	332
	  }


    static SQLiteClient m_db = null;
	static MusicDatabase()
		{
			Open();
		}
	static void Open()
		{
      Log.WriteFile(Log.LogType.Log,false,"Opening music database");
      try 
      {
				// Open database

				String strPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.
					GetExecutingAssembly().Location); 
				try
				{
					System.IO.Directory.CreateDirectory(strPath+@"\database");
				}
				catch(Exception){}
        m_db = new SQLiteClient(strPath+@"\database\musicdatabase4.db");
        CreateTables();

        m_db.Execute("PRAGMA cache_size=8192\n");
        m_db.Execute("PRAGMA synchronous='OFF'\n");
		m_db.Execute("PRAGMA count_changes='OFF'\n");
      } 
      catch (Exception ex) 
      {
        Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
      }
      Log.WriteFile(Log.LogType.Log,false,"music database opened");
    }

    ~MusicDatabase()
    {
    }

	static public SQLiteClient DBHandle
		{
		get { return m_db; }
		}

    static bool CreateTables()
    {
      if (m_db == null) return false;
      DatabaseUtility.AddTable(m_db,"artist","CREATE TABLE artist ( idArtist integer primary key, strArtist text)\n");
      DatabaseUtility.AddTable(m_db,"album","CREATE TABLE album ( idAlbum integer primary key, idArtist integer, strAlbum text)\n");
      DatabaseUtility.AddTable(m_db,"genre","CREATE TABLE genre ( idGenre integer primary key, strGenre text)\n");
      DatabaseUtility.AddTable(m_db,"path","CREATE TABLE path ( idPath integer primary key,  strPath text)\n");
      DatabaseUtility.AddTable(m_db,"albuminfo","CREATE TABLE albuminfo ( idAlbumInfo integer primary key, idAlbum integer, idArtist integer,iYear integer, idGenre integer, strTones text, strStyles text, strReview text, strImage text, strTracks text, iRating integer)\n");
	  DatabaseUtility.AddTable(m_db,"artistinfo","CREATE TABLE artistinfo ( idArtistInfo integer primary key, idArtist integer, strBorn text, strYearsActive text, strGenres text, strTones text, strStyles text, strInstruments text, strImage text, strAMGBio text, strAlbums text, strCompilations text, strSingles text, strMisc text)\n");
      DatabaseUtility.AddTable(m_db,"song","CREATE TABLE song ( idSong integer primary key, idArtist integer, idAlbum integer, idGenre integer, idPath integer, strTitle text, iTrack integer, iDuration integer, iYear integer, dwFileNameCRC text, strFileName text, iTimesPlayed integer, iRating integer, favorite integer)\n");
	  return true;
    }

    public int AddPath(string strPath1)
    {
      string strSQL;
      try
      {
        if (strPath1 == null) return - 1;
        if (strPath1.Length == 0) return - 1;
        string strPath = strPath1;
        //	musicdatabase always stores directories 
        //	without a slash at the end 
        if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
        strPath = strPath.Substring(0, strPath.Length - 1);
        DatabaseUtility.RemoveInvalidChars(ref strPath);

        if (null == m_db) return - 1;

        foreach (CPathCache path in m_pathCache)
        {
          if (path.strPath == strPath1)
          {
            return path.idPath;
          }
        }

        SQLiteResultSet results;
        strSQL = String.Format("select * from path where strPath like '{0}'", strPath);
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) 
        {
          // doesnt exists, add it
          strSQL = String.Format("insert into path (idPath, strPath) values ( NULL, '{0}' )", strPath);
          m_db.Execute(strSQL);

          CPathCache path = new CPathCache();
          path.idPath = m_db.LastInsertID();
          path.strPath = strPath1;
          m_pathCache.Add(path);
          return path.idPath;
        }
        else
        {
          CPathCache path = new CPathCache();
          path.idPath = Int32.Parse(DatabaseUtility.Get(results, 0, "idPath"));
          path.strPath = strPath1;
          m_pathCache.Add(path);
          return path.idPath;
        }
      } 
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return - 1;
    }

    public int AddArtist(string strArtist1)
    {
      string strSQL;
      try 
      {
        string strArtist = strArtist1;
        DatabaseUtility.RemoveInvalidChars(ref strArtist);

        if (null == m_db) return - 1;
        foreach (CArtistCache artist in m_artistCache)
        {
          if (artist.strArtist == strArtist1)
            return artist.idArtist;
        }
        strSQL = String.Format("select * from artist where strArtist like '{0}'", strArtist);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) 
        {
          // doesnt exists, add it
          strSQL = String.Format("insert into artist (idArtist, strArtist) values( NULL, '{0}' )", strArtist);
          m_db.Execute(strSQL);
          CArtistCache artist = new CArtistCache();
          artist.idArtist = m_db.LastInsertID();
          artist.strArtist = strArtist1;
          m_artistCache.Add(artist);
          return artist.idArtist;
        }
        else
        {
          CArtistCache artist = new CArtistCache();
          artist.idArtist = Int32.Parse(DatabaseUtility.Get(results, 0, "idArtist"));
          artist.strArtist = strArtist1;
          m_artistCache.Add(artist);
          return artist.idArtist;
        }
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return - 1;
    }

    public int AddGenre(string strGenre1)
    {
      string strSQL;
      try
      {
        string strGenre = strGenre1;
        DatabaseUtility.RemoveInvalidChars(ref strGenre);

        if (null == m_db) return - 1;
        foreach (CGenreCache genre in m_genreCache)
        {
          if (genre.strGenre == strGenre1)
            return genre.idGenre;
        }
        strSQL = String.Format("select * from genre where strGenre like '{0}'", strGenre);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) 
        {
          // doesnt exists, add it
          strSQL = String.Format("insert into genre (idGenre, strGenre) values( NULL, '{0}' )", strGenre);
          m_db.Execute(strSQL);

          CGenreCache genre = new CGenreCache();
          genre.idGenre = m_db.LastInsertID();
          genre.strGenre = strGenre1;
          m_genreCache.Add(genre);
          return genre.idGenre;
        }
        else
        {
          CGenreCache genre = new CGenreCache();
          genre.idGenre = Int32.Parse(DatabaseUtility.Get(results, 0, "idGenre"));
          genre.strGenre = strGenre1;
          m_genreCache.Add(genre);
          return genre.idGenre;
        }
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return - 1;
    }
	public void SetFavorite(Song song)
		{
			try
			{
				if (song.songId < 0) return;
				int iFavorite=0;
				if (song.Favorite) iFavorite=1;
				string strSQL = String.Format("update song set favorite={0} where idSong={1}",iFavorite, song.songId);
				m_db.Execute(strSQL);
				return ;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}
		}

	public void SetRating(string filename, int rating)
		{
				try
					{
						Song song = new Song();
						string strFileName = filename;
						DatabaseUtility.RemoveInvalidChars(ref strFileName);

						string strPath, strFName;
						DatabaseUtility.Split(strFileName, out strPath, out strFName);
						if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
							strPath = strPath.Substring(0, strPath.Length - 1);

						if (null == m_db) return ;
				
						string strSQL;
						ulong dwCRC;
						CRCTool crc = new CRCTool();
						crc.Init(CRCTool.CRCCode.CRC32);
						dwCRC = crc.calc(filename);

						strSQL = String.Format("select * from song,path where song.idPath=path.idPath and dwFileNameCRC='{0}' and strPath='{1}'",
							dwCRC, 
							strPath);
						SQLiteResultSet results;
						results = m_db.Execute(strSQL);
						if (results.Rows.Count == 0) return ;
						int idSong = Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));

						strSQL = String.Format("update song set iRating={0} where idSong={1}",
							rating, idSong);
						m_db.Execute(strSQL);
						return ;
					}
					catch (Exception ex)
					{
					Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
					Open();
					}

			return ;
		}
		
	public SQLiteResultSet GetResults(string sql)
		{
			try
			{
				if (null == m_db) return null;
				SQLiteResultSet results;
				results = m_db.Execute(sql);
				return results;
			}
			catch (Exception ex) 
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return null;	
		}

	public void GetSongsByFilter(string sql, out ArrayList songs, bool artistTable, bool albumTable, bool songTable, bool genreTable)
		{
			songs=new ArrayList();
			try
			{
				if (null == m_db) return ;
				//Originele regel
				//SQLiteResultSet results=GetResults(sql);
				//Nieuwe regel
				SQLiteResultSet results=m_db.Execute(sql);

				MediaPortal.Music.Database.Song song;
				//Log.Write (sql);
				//Log.Write ("Aantal rijen = {0}",(int)results.Rows.Count);

				for (int i=0; i<results.Rows.Count; i++)
				{
					song = new Song();
					ArrayList fields = (ArrayList)results.Rows[i];
					if (artistTable && !songTable)
					{
						song.Artist = (string)fields[1];
						song.artistId= (int)Math.Floor(0.5d+Double.Parse((string)fields[0]));
						//Log.Write ("artisttable and not songtable, artistid={0}",song.artistId);
					}
					if (albumTable && !songTable)
					{
						song.Album =  (string)fields[2];
						///TFRO 11-6-2005
						///The 2 lines below don't always give the right answer
						///Todo: find out how to get a int from the db WITHOUT decimals
						song.albumId = (int)Math.Floor(0.5d+Double.Parse((string)fields[0]));
						song.artistId= (int)Math.Floor(0.5d+Double.Parse((string)fields[1]));
						///So we replace this immediatly with some other code
						NumberFormatInfo nfi = new CultureInfo( "en-US", false ).NumberFormat;
						nfi.NumberDecimalSeparator = ".";

						song.albumId=Convert.ToInt32(Math.Round(Double.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"),nfi)));
						song.artistId=Convert.ToInt32(Math.Round(Double.Parse(DatabaseUtility.Get(results, i, "album.idArtist"),nfi)));

						if (fields.Count>=5)
							song.Artist = (string)fields[4];
					}
					if (genreTable && !songTable)
					{
						song.Genre = (string)fields[1];
						song.genreId = (int)Math.Floor(0.5d+Double.Parse((string)fields[0]));
						//Log.Write ("genretable and not songtable, genreid={0}",song.genreId);
					}
					if (songTable)
					{
						song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
						song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
						song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
						song.artistId= (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.idArtist")));
						song.Track = (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.iTrack")));
						song.Duration = (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.iDuration")));
						song.Year = (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.iYear")));
						song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
						song.TimesPlayed = (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed")));
						song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
						song.Rating= (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.iRating")));
						song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
						song.songId= (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.idSong")));
						song.albumId= (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.idAlbum")));
						song.genreId= (int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.idGenre")));
						string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
						strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
						song.FileName = strFileName;
						//Log.Write ("Song table with albumid={0}, artistid={1},songid={2}, strFilename={3}",song.albumId,song.artistId,song.songId,song.FileName);
					}
					songs.Add(song);
				}	  

				return ;
			}
			catch (Exception ex) 
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return ;		
		}

		
    public int AddAlbum(string strAlbum1, int lArtistId)
    {
      string strSQL;
      try
      {
        string strAlbum = strAlbum1;
        DatabaseUtility.RemoveInvalidChars(ref strAlbum);

        if (null == m_db) return - 1;
        foreach (AlbumInfoCache album in m_albumCache)
        {
          if (strAlbum1 == album.Album)
            return album.idAlbum;
        }

        strSQL = String.Format("select * from album where strAlbum like '{0}'", strAlbum);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);

        if (results.Rows.Count == 0) 
        {
          // doesnt exists, add it
          strSQL = String.Format("insert into album (idAlbum, strAlbum,idArtist) values( NULL, '{0}', {1})", strAlbum, lArtistId);
          m_db.Execute(strSQL);

          AlbumInfoCache album = new AlbumInfoCache();
          album.idAlbum = m_db.LastInsertID();
          album.Album = strAlbum1;
          album.idArtist = lArtistId;
          m_albumCache.Add(album);
          return album.idAlbum;
        }
        else
        {
          AlbumInfoCache album = new AlbumInfoCache();
          album.idAlbum = Int32.Parse(DatabaseUtility.Get(results, 0, "idAlbum"));
          album.Album = strAlbum1;
          album.idArtist = Int32.Parse(DatabaseUtility.Get(results, 0, "idArtist"));
          m_albumCache.Add(album);
          return album.idAlbum;
        }
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return - 1;
    }

    public void EmptyCache()
    {
      m_artistCache.Clear();
      m_genreCache.Clear();
      m_pathCache.Clear();
      m_albumCache.Clear();
    }

    public bool IsOpen
    {
      get { return m_db != null; }
    }


	///TFRO71 4 june 2005 
	///This part is not used by the database class itself
	///But the wizard_selectplugins somehow uses this part to add songs
	///Weird right?
	public void AddSong(Song song1, bool bCheck)
    {
      //Log.WriteFile(Log.LogType.Log,true,"database.AddSong {0} {1} {2}  {3}", song1.FileName,song1.Album, song1.Artist, song1.Title);
      string strSQL;
      try
      {
        Song song = song1.Clone();
        string strTmp;
  	    Log.Write ("MusicDatabaseReorg: Going to AddSong {0}",song.FileName);

//        strTmp = song.Album; DatabaseUtility.RemoveInvalidChars(ref strTmp); song.Album = strTmp;
//        strTmp = song.Genre; DatabaseUtility.RemoveInvalidChars(ref strTmp); song.Genre = strTmp;
//        strTmp = song.Artist; DatabaseUtility.RemoveInvalidChars(ref strTmp); song.Artist = strTmp;
        strTmp = song.Title; DatabaseUtility.RemoveInvalidChars(ref strTmp); song.Title = strTmp;
		strTmp = song.FileName; DatabaseUtility.RemoveInvalidChars(ref strTmp); song.FileName = strTmp;

        string strPath, strFileName;

		  DatabaseUtility.Split(song.FileName, out strPath, out strFileName);

        if (null == m_db) return;
        int lGenreId = AddGenre(song.Genre);
        int lArtistId = AddArtist(song.Artist);
        int lPathId = AddPath(strPath);
        int lAlbumId = AddAlbum(song.Album, lArtistId);

		Log.Write ("Getting a CRC for {0}",song.FileName);

        ulong dwCRC = 0;
        CRCTool crc = new CRCTool();
        crc.Init(CRCTool.CRCCode.CRC32);
        dwCRC = crc.calc(song.FileName);
        SQLiteResultSet results;

	    Log.Write ("MusicDatabaseReorg: CRC for {0} = {1}",song.FileName,dwCRC);
        if (bCheck)
        {
          strSQL = String.Format("select * from song where idAlbum={0} AND idGenre={1} AND idArtist={2} AND dwFileNameCRC='{3}' AND strTitle='{4}'", 
                                lAlbumId, lGenreId, lArtistId, dwCRC, song.Title);
		  Log.Write (strSQL);
			try
			{
				results = m_db.Execute(strSQL);

				song1.albumId=lAlbumId;
				song1.artistId=lArtistId;
				song1.genreId=lGenreId;

				if (results.Rows.Count != 0)  
				{
					song1.songId=Int32.Parse(DatabaseUtility.Get(results,0,"idSong"));
					return;
				}
	
			
			}
			catch (Exception ex) 
			{
				Log.Write ("MusicDatabaseReorg: Executing query failed");
			}
		} //End if

    	int iFavorite=0;
		if (song.Favorite) iFavorite=1;

		crc.Init(CRCTool.CRCCode.CRC32);
		dwCRC = crc.calc(strFileName);

		  Log.Write ("Song {0} will be added with CRC {1}",strFileName,dwCRC);

        strSQL = String.Format("insert into song (idSong,idArtist,idAlbum,idGenre,idPath,strTitle,iTrack,iDuration,iYear,dwFileNameCRC,strFileName,iTimesPlayed,iRating,favorite) values(NULL,{0},{1},{2},{3},'{4}',{5},{6},{7},'{8}','{9}',{10},{11},{12})",
          lArtistId, lAlbumId, lGenreId, lPathId, 
          song.Title, 
          song.Track, song.Duration, song.Year, 
          dwCRC, 
          strFileName, 0,song.Rating, iFavorite);
		song1.songId=m_db.LastInsertID();


        m_db.Execute(strSQL);
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }
    }

	public void DeleteSong(string strFileName, bool bCheck)
		{
			try
			{
				int lGenreId = -1;
				int lArtistId = -1;
				int lPathId = -1;
				int lAlbumId = -1;
				int lSongId = -1;
			
				DatabaseUtility.RemoveInvalidChars(ref strFileName);

				string strPath, strFName;
				DatabaseUtility.Split(strFileName, out strPath, out strFName);

				if (null == m_db) return;

				CRCTool crc = new CRCTool();
				crc.Init(CRCTool.CRCCode.CRC32);
				ulong dwCRC = crc.calc(strFileName);

				string strSQL;
				strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and dwFileNameCRC='{0}' and strPath='{1}'",
					dwCRC, 
					strPath);

				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count > 0)
				{
					lArtistId= Int32.Parse(DatabaseUtility.Get(results, 0, "artist.idArtist"));
					lAlbumId= Int32.Parse(DatabaseUtility.Get(results, 0, "album.idAlbum"));
					lGenreId= Int32.Parse(DatabaseUtility.Get(results, 0, "genre.idGenre"));
					lPathId= Int32.Parse(DatabaseUtility.Get(results, 0, "path.idPath"));
					lSongId= Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));
				
					// Delete
					strSQL = String.Format("delete from song where song.idSong={0}", lSongId);
					m_db.Execute(strSQL);

					if (bCheck)
					{
						// Check albums
						strSQL = String.Format("select * from song where song.idAlbum={0}", lAlbumId);					
						results = m_db.Execute(strSQL);
						if (results.Rows.Count == 0)
						{
							// Delete album with no songs
							strSQL = String.Format("delete from album where idAlbum={0}",lAlbumId);
							m_db.Execute(strSQL);

							// Delete album info
							strSQL = String.Format("delete from albuminfo where idAlbum={0}",lAlbumId);
							m_db.Execute(strSQL);
						}
					
						// Check artists
						strSQL = String.Format("select * from song where song.idArtist={0}", lArtistId);
						results = m_db.Execute(strSQL);
						if (results.Rows.Count == 0) 
						{					
							// Delete artist with no songs
							strSQL = String.Format("delete from artist where idArtist={0}", lArtistId);
							m_db.Execute(strSQL);

							// Delete artist info
							strSQL = String.Format("delete from artistinfo where idArtist={0}", lArtistId);
							m_db.Execute(strSQL);
						}

						// Check path
						strSQL = String.Format("select * from song where song.idPath={0}", lPathId);
						results = m_db.Execute(strSQL);
						if (results.Rows.Count == 0) 
						{
							// Delete path with no songs
							strSQL = String.Format("delete from path where idPath={0}", lPathId);
							m_db.Execute(strSQL);
						
							// remove from cache
							foreach (CPathCache path in m_pathCache)
							{
								if (path.idPath == lPathId)
								{
									int iIndex=m_pathCache.IndexOf(path);
									if (iIndex!=-1)
									{
										m_pathCache.RemoveAt(iIndex);
									}
								}
							}
						}

						// Check genre
						strSQL = String.Format("select * from song where song.idGenre={0}", lGenreId);
						results = m_db.Execute(strSQL);
						if (results.Rows.Count == 0) 
						{
							// delete genre with no songs
							strSQL = String.Format("delete from genre where idGenre={0}", lGenreId);
							m_db.Execute(strSQL);
						}				
					}
				}
				return;
			}
			catch (Exception ex) 
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return;
		}

    public bool GetSongByFileName(string strFileName1, ref Song song)
    {
	    try
	    {
		    song.Clear();
		    string strFileName = strFileName1;
		    DatabaseUtility.RemoveInvalidChars(ref strFileName);

		    string strPath, strFName;
		    DatabaseUtility.Split(strFileName, out strPath, out strFName);

		    if (null == m_db) return false;

        CRCTool crc = new CRCTool();
        crc.Init(CRCTool.CRCCode.CRC32);
        ulong dwCRC = crc.calc(strFileName1);

        string strSQL;
		    strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and dwFileNameCRC='{0}' and strPath='{1}'",
										          dwCRC, 
										          strPath);

        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) return false;
		    song.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
		    song.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
		    song.Genre = DatabaseUtility.Get(results, 0, "genre.strGenre");
		    song.Track = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTrack"));
		    song.Duration = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iDuration"));
		    song.Year = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iYear"));
		    song.Title = DatabaseUtility.Get(results, 0, "song.strTitle");
		    song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTimesPlayed"));
				song.Rating= Int32.Parse(DatabaseUtility.Get(results, 0, "song.iRating"));
				song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, 0, "song.favorite")))!=0;
				song.songId= Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));
				song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, 0, "song.favorite")))!=0;
				song.artistId= Int32.Parse(DatabaseUtility.Get(results, 0, "artist.idArtist"));
				song.albumId= Int32.Parse(DatabaseUtility.Get(results, 0, "album.idAlbum"));
				song.genreId= Int32.Parse(DatabaseUtility.Get(results, 0, "genre.idGenre"));
		    song.FileName = strFileName1;
		    return true;
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

	    return false;
    }

    public bool GetSong(string strTitle1, ref Song song)
    {
	    try
	    {
		    song.Clear();
		    string strTitle = strTitle1;
		    DatabaseUtility.RemoveInvalidChars(ref strTitle);

		    if (null == m_db) return false;

		    string strSQL;
		    strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and strTitle='{0}'",strTitle);

        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) return false;

		    song.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
		    song.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
		    song.Genre = DatabaseUtility.Get(results, 0, "genre.strGenre");
		    song.Track = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTrack"));
		    song.Duration = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iDuration"));
		    song.Year = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iYear"));
		    song.Title = DatabaseUtility.Get(results, 0, "song.strTitle");
		    song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTimesPlayed"));
				song.Rating= Int32.Parse(DatabaseUtility.Get(results, 0, "song.iRating"));
				song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, 0, "song.favorite")))!=0;;
				song.songId= Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));
				song.artistId= Int32.Parse(DatabaseUtility.Get(results, 0, "artist.idArtist"));
				song.albumId= Int32.Parse(DatabaseUtility.Get(results, 0, "album.idAlbum"));
				song.genreId= Int32.Parse(DatabaseUtility.Get(results, 0, "genre.idGenre"));
		    string strFileName = DatabaseUtility.Get(results, 0, "path.strPath");
		    strFileName += DatabaseUtility.Get(results, 0, "song.strFileName");
		    song.FileName = strFileName;
		    return true;
      }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

	    return false;
    }
//added by Sam
	public bool GetRandomSong(ref Song song)
	  {
		  try
		  {
			  song.Clear();

			  if (null == m_db) return false;

			  string strSQL;
			  int maxIDSong, rndIDSong;
			  strSQL = String.Format("select * from song ORDER BY idSong DESC LIMIT 1");
			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  maxIDSong = Int32.Parse(DatabaseUtility.Get(results,0,"idSong"));
			  rndIDSong = new System.Random().Next(maxIDSong);

			  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and idSong={0}",rndIDSong);

			  results = m_db.Execute(strSQL);
			  if (results.Rows.Count > 0) 
			  {
				  song.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
				  song.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
				  song.Genre = DatabaseUtility.Get(results, 0, "genre.strGenre");
				  song.Track = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTrack"));
				  song.Duration = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iDuration"));
				  song.Year = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iYear"));
				  song.Title = DatabaseUtility.Get(results, 0, "song.strTitle");
				  song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTimesPlayed"));
					song.Rating= Int32.Parse(DatabaseUtility.Get(results, 0, "song.iRating"));
					song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, 0, "song.favorite")))!=0;
					song.songId= Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));
					song.artistId= Int32.Parse(DatabaseUtility.Get(results, 0, "artist.idArtist"));
					song.albumId= Int32.Parse(DatabaseUtility.Get(results, 0, "album.idAlbum"));
					song.genreId= Int32.Parse(DatabaseUtility.Get(results, 0, "genre.idGenre"));
				  string strFileName = DatabaseUtility.Get(results, 0, "path.strPath");
				  strFileName += DatabaseUtility.Get(results, 0, "song.strFileName");
				  song.FileName = strFileName;
				  return true;
			  }
			  else
			  {
				  GetRandomSong(ref song);
				  return true;
			  }
			  
		  }
		  catch (Exception ex) 
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }

		  return false;
	  }

	  //added by Sam
	public int GetNumOfSongs()
	  {
		try
		  {
			  if (null == m_db) return 0;

			  string strSQL;
			  int NumOfSongs;
			  strSQL = String.Format("select count(*) from song");
			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  ArrayList row = (ArrayList)results.Rows[0];
			  NumOfSongs = Int32.Parse((string)row[0]);
			  return NumOfSongs;
		  }
		  catch (Exception ex) 
		  {
			Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
			Open();
		  }

	  return 0;

	  }
    
	  //added by Sam
	public bool GetAllSongs(ref ArrayList songs)
	  {
		  try
		  {
			  if (null == m_db) return false;

			  string strSQL;
			  SQLiteResultSet results;
			  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist");

			  results = m_db.Execute(strSQL);
			  Song song;

			  for (int i=0; i<results.Rows.Count; i++)
			  {
				song = new Song();
		        song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
				song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
				song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
				song.Track = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTrack"));
				song.Duration = Int32.Parse(DatabaseUtility.Get(results, i, "song.iDuration"));
				song.Year = Int32.Parse(DatabaseUtility.Get(results, i, "song.iYear"));
				song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
				song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed"));
				song.Rating= Int32.Parse(DatabaseUtility.Get(results, i, "song.iRating"));
				song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
				song.songId= Int32.Parse(DatabaseUtility.Get(results, i, "song.idSong"));
				song.artistId= Int32.Parse(DatabaseUtility.Get(results, i, "artist.idArtist"));
				song.albumId= Int32.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"));
				song.genreId= Int32.Parse(DatabaseUtility.Get(results, i, "genre.idGenre"));
				string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
				strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
				song.FileName = strFileName;
				songs.Add(song);
			  }	  

			  return true;
		  }
		  catch (Exception ex) 
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }

		  return false;
	  }
    public bool GetSongsByArtist(string strArtist1, ref ArrayList songs)
    {
	    try
	    {
		    string strArtist = strArtist1;
		    DatabaseUtility.RemoveInvalidChars(ref strArtist);

		    songs.Clear();
		    if (null == m_db) return false;
    		
		    string strSQL;
		    strSQL = String.Format("select song.idSong,artist.idArtist,album.idAlbum,genre.idGenre,song.favorite,song.strTitle, song.iYear, song.iDuration, song.iTrack, song.iTimesPlayed, song.strFileName, song.iRating, path.strPath, genre.strGenre, album.strAlbum, artist.strArtist from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and artist.strArtist like '{0}'",strArtist);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
		    if (results.Rows.Count == 0) return false;
		    for (int i = 0; i < results.Rows.Count; ++i)
		    {
			    Song song = new Song();
			    song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
			    song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
			    song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
			    song.Track = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTrack"));
			    song.Duration = Int32.Parse(DatabaseUtility.Get(results, i, "song.iDuration"));
			    song.Year = Int32.Parse(DatabaseUtility.Get(results, i, "song.iYear"));
			    song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
			    song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed"));
					song.Rating= Int32.Parse(DatabaseUtility.Get(results, i, "song.iRating"));
					song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
			    string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
			    strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
					song.songId= Int32.Parse(DatabaseUtility.Get(results, i, "song.idSong"));
					song.artistId= Int32.Parse(DatabaseUtility.Get(results, i, "artist.idArtist"));
					song.albumId= Int32.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"));
					song.genreId= Int32.Parse(DatabaseUtility.Get(results, i, "genre.idGenre"));
			    song.FileName = strFileName;
			    songs.Add(song);
		    }

		    return true;
	    }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

	    return false;
    }
	  //
	public bool GetSongs(int searchKind,string strTitle1,ref ArrayList songs)
	  {
		  try
		  {
			  songs.Clear();
			  string strTitle=strTitle1;
			  if (null == m_db) 
				  return false;
    		
			  string strSQL=String.Empty;
			  switch (searchKind)
			  {
				  case 0:
					  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and strTitle like '{0}%'",strTitle);
					  break;
				  case 1:
					  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and strTitle like '%{0}%'",strTitle);
					  break;
				  case 2:
					  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and strTitle like '%{0}'",strTitle);
					  break;
				  case 3:
					  strSQL = String.Format("select * from song,album,genre,artist,path where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and strTitle like '{0}'",strTitle);
					  break;
					default:
						return false;
			  }
			  
			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  if (results.Rows.Count == 0) 
				  return false;
			  for (int i = 0; i < results.Rows.Count; ++i)
			  {
				  string strFileName=DatabaseUtility.Get(results, i, "path.strPath");
				  strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
				  GUIListItem item = new GUIListItem();
				  item.IsFolder = false;
				  item.Label = Utils.GetFilename(strFileName);
				  item.Label2 = String.Empty;
				  item.Label3 = String.Empty;
				  item.Path = strFileName;
				  item.FileInfo = new FileInformation(strFileName);
				  Utils.SetDefaultIcons(item);
				  Utils.SetThumbnails(ref item);
				  songs.Add(item);
			  }
			  return true;
		  }
		  catch (Exception ex) 
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }
		  return false;
	  }
	  //
	public bool GetArtists(int searchKind,string strArtist1,ref ArrayList artists)
	  {
		  try
		  {
			  artists.Clear();
			  string strArtist2=strArtist1;
			  if (null == m_db) return false;
    		
			  // Exclude "Various Artists"
			  string strVariousArtists = GUILocalizeStrings.Get(340);
			  long lVariousArtistId = AddArtist(strVariousArtists);
			  string strSQL=String.Empty;
			  switch (searchKind)
			  {
				  case 0:
					  strSQL = String.Format("select * from artist where strArtist like '{0}%' ", strArtist2);
					  break;
				  case 1:
					  strSQL = String.Format("select * from artist where strArtist like '%{0}%' ", strArtist2);
					  break;
				  case 2:
					  strSQL = String.Format("select * from artist where strArtist like '%{0}' ", strArtist2);
					  break;
				  case 3:
					  strSQL = String.Format("select * from artist where strArtist like '{0}' ", strArtist2);
					  break;
					default:
						return false;
			  }

			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  if (results.Rows.Count == 0) return false;
			  for (int i = 0; i < results.Rows.Count; ++i)
			  {
				  string strArtist = DatabaseUtility.Get(results, i, "strArtist");
				  artists.Add(strArtist);
			  }

			  return true;
		  }
		  catch (Exception ex) 
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }

		  return false;
	  }
	  //
    public bool GetArtists(ref ArrayList artists)
    {
	    try
	    {
		    artists.Clear();

		    if (null == m_db) return false;
    		

		    // Exclude "Various Artists"
		    string strVariousArtists = GUILocalizeStrings.Get(340);
		    long lVariousArtistId = AddArtist(strVariousArtists);
		    string strSQL;
		    strSQL = String.Format("select * from artist where idArtist <> {0} ", lVariousArtistId);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) return false;
        for (int i = 0; i < results.Rows.Count; ++i)
        {
			    string strArtist = DatabaseUtility.Get(results, i, "strArtist");
			    artists.Add(strArtist);
		    }

		    return true;
	    }
      catch (Exception ex) 
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

	    return false;
    }

    public bool GetAlbums(ref ArrayList albums)
    {
      try
      {
        albums.Clear();
        if (null == m_db) return false;
    		
        string strSQL;
        strSQL = String.Format("select * from album,artist where album.idArtist=artist.idArtist");
        //strSQL=String.Format("select distinct album.idAlbum, album.idArtist, album.strAlbum, artist.idArtist, artist.strArtist from album,artist,song where song.idArtist=artist.idArtist and song.idAlbum=album.idAlbum");
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) return false;
        for (int i = 0; i < results.Rows.Count; ++i)
        {
			    AlbumInfo album = new AlbumInfo();
			    album.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
			    album.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
			    albums.Add(album);
		    }
		    return true;
	    }
	    catch (Exception ex)
	    {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
	    }

	    return false;
    }
	public bool GetAlbums(int searchKind,string strAlbum1,ref ArrayList albums)
	  {
		  try
		  {
			  string strAlbum=strAlbum1;
			  albums.Clear();
			  if (null == m_db) return false;
    		
			  string strSQL=String.Empty;
			  switch (searchKind)
			  {
				  case 0:
					  strSQL = String.Format("select * from album,artist where album.idArtist=artist.idArtist and album.strAlbum like '{0}%'",strAlbum);
					  break;
				  case 1:
					  strSQL = String.Format("select * from album,artist where album.idArtist=artist.idArtist and album.strAlbum like '%{0}%'",strAlbum);
					  break;
				  case 2:
					  strSQL = String.Format("select * from album,artist where album.idArtist=artist.idArtist and album.strAlbum like '%{0}'",strAlbum);
					  break;
				  case 3:
					  strSQL = String.Format("select * from album,artist where album.idArtist=artist.idArtist and album.strAlbum like '{0}'",strAlbum);
					  break;
					default:
						return false;
			  }
					  //strSQL=String.Format("select distinct album.idAlbum, album.idArtist, album.strAlbum, artist.idArtist, artist.strArtist from album,artist,song where song.idArtist=artist.idArtist and song.idAlbum=album.idAlbum");
			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  if (results.Rows.Count == 0) return false;
			  for (int i = 0; i < results.Rows.Count; ++i)
			  {
				  AlbumInfo album = new AlbumInfo();
				  album.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
				  album.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
				  albums.Add(album);
			  }
			  return true;
		  }
		  catch (Exception ex)
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }

		  return false;
	  }
    public bool GetGenres(ref ArrayList genres)
    {
	    try
	    {
		    genres.Clear();
		    if (null == m_db) return false;
		    string strSQL;
		    strSQL = String.Format("select * from genre");
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count == 0) return false;
        for (int i = 0; i < results.Rows.Count; ++i)
		    {
			    string strGenre = DatabaseUtility.Get(results, i, "strGenre");
			    genres.Add(strGenre);
		    }

		    return true;
	    }
	    catch (Exception ex)
	    {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }
      return false;
    }
	  //
	public bool GetGenres(int searchKind,string strGenere1,ref ArrayList genres)
	  {
		  try
		  {
			  genres.Clear();
			  string strGenere=strGenere1;
			  if (null == m_db) return false;
			  string strSQL=String.Empty;
			  switch (searchKind)
			  {
				  case 0:
					  strSQL = String.Format("select * from genre where strGenre like '{0}%'",strGenere);
					  break;
				  case 1:
					  strSQL = String.Format("select * from genre where strGenre like '%{0}%'",strGenere);
					  break;
				  case 2:
					  strSQL = String.Format("select * from genre where strGenre like '%{0}'",strGenere);
					  break;
				  case 3:
					  strSQL = String.Format("select * from genre where strGenre like '{0}'",strGenere);
					  break;
					default:
						return false;
			  }
			  SQLiteResultSet results;
			  results = m_db.Execute(strSQL);
			  if (results.Rows.Count == 0) return false;
			  for (int i = 0; i < results.Rows.Count; ++i)
			  {
				  string strGenre = DatabaseUtility.Get(results, i, "strGenre");
				  genres.Add(strGenre);
			  }

			  return true;
		  }
		  catch (Exception ex)
		  {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
		  }
		  return false;
	  }
	public bool GetSongsByPath(string strPath1, ref ArrayList songs)
		{
			try
			{
        songs.Clear();
        if (strPath1 == null) return false;
        if (strPath1.Length == 0) return false;
				string strPath = strPath1;
				//	musicdatabase always stores directories 
				//	without a slash at the end 
				if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
					strPath = strPath.Substring(0, strPath.Length - 1);
				DatabaseUtility.RemoveInvalidChars(ref strPath);
				if (null == m_db) return false;
				
				string strSQL;
				strSQL = String.Format("select * from song,path,album,genre,artist where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and path.strPath like '{0}'",strPath);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count == 0) return false;
				for (int i = 0; i < results.Rows.Count; ++i)
				{
					Song song = new Song();
					song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
					song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
					song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
					song.Track = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTrack"));
					song.Duration = Int32.Parse(DatabaseUtility.Get(results, i, "song.iDuration"));
					song.Year = Int32.Parse(DatabaseUtility.Get(results, i, "song.iYear"));
					song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
					song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed"));
					song.Rating= Int32.Parse(DatabaseUtility.Get(results, i, "song.iRating"));
					song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
					song.songId= Int32.Parse(DatabaseUtility.Get(results, i, "song.idSong"));
					song.artistId= Int32.Parse(DatabaseUtility.Get(results, i, "artist.idArtist"));
					song.albumId= Int32.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"));
					song.genreId= Int32.Parse(DatabaseUtility.Get(results, i, "genre.idGenre"));					
					string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
					strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
					song.FileName = strFileName;
					
					songs.Add(song);
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}
			return false;
		}

		
	public bool GetRecentlyPlayedAlbums(ref ArrayList albums)
    {
			try
			{
				albums.Clear();
				if (null == m_db) return false;
				
				string strSQL;
				strSQL = String.Format("select distinct album.*, artist.*, path.* from album,artist,path,song where album.idAlbum=song.idAlbum and album.idArtist=artist.idArtist and song.idPath=path.idPath and song.iTimesPlayed > 0 order by song.iTimesPlayed limit 20");
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count == 0) return false;
				for (int i = 0; i < results.Rows.Count; ++i)
				{
					AlbumInfo album = new AlbumInfo();
					album.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
					album.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
					albums.Add(album);
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}
			return false;
    }

    public void ResetTop100()
    {
			try
			{
        string strSQL = String.Format("update song set iTimesPlayed=0");
        m_db.Execute(strSQL);
      }
      catch (Exception ex)
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }
    }

	public bool IncrTop100CounterByFileName(string strFileName1)
		{
			try
			{
				Song song = new Song();
				string strFileName = strFileName1;
				DatabaseUtility.RemoveInvalidChars(ref strFileName);

				string strPath, strFName;
				DatabaseUtility.Split(strFileName, out strPath, out strFName);
        if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
          strPath = strPath.Substring(0, strPath.Length - 1);

				if (null == m_db) return false;
				
				string strSQL;
				ulong dwCRC;
				CRCTool crc = new CRCTool();
				crc.Init(CRCTool.CRCCode.CRC32);
				dwCRC = crc.calc(strFileName1);

				strSQL = String.Format("select * from song,path where song.idPath=path.idPath and dwFileNameCRC='{0}' and strPath='{1}'",
														dwCRC, 
														strPath);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count == 0) return false;
				int idSong = Int32.Parse(DatabaseUtility.Get(results, 0, "song.idSong"));
				int iTimesPlayed = Int32.Parse(DatabaseUtility.Get(results, 0, "song.iTimesPlayed"));

				strSQL = String.Format("update song set iTimesPlayed={0} where idSong={1}",
															++iTimesPlayed, idSong);
				m_db.Execute(strSQL);
				return true;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return false;
		}

    public int	AddAlbumInfo(AlbumInfo album1)
    {
			string strSQL;
			try
			{
				AlbumInfo album = album1.Clone();
				string strTmp;
//				strTmp = album.Album; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Album = strTmp;
//				strTmp = album.Genre; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Genre = strTmp;
//				strTmp = album.Artist; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Artist = strTmp;
				strTmp = album.Tones; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Tones = strTmp;
				strTmp = album.Styles; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Styles = strTmp;
				strTmp = album.Review; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Review = strTmp;
				strTmp = album.Image; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Image = strTmp;
        strTmp = album.Tracks; DatabaseUtility.RemoveInvalidChars(ref strTmp); album.Tracks = strTmp;
        //strTmp=album.Path  ;RemoveInvalidChars(ref strTmp);album.Path=strTmp;

				if (null == m_db) return - 1;
				int lGenreId = AddGenre(album1.Genre);
				//int lPathId   = AddPath(album1.Path);
				int lArtistId = AddArtist(album1.Artist);
				int lAlbumId = AddAlbum(album1.Album, lArtistId);

        strSQL = String.Format("delete  from albuminfo where idAlbum={0} ", lAlbumId);
        m_db.Execute(strSQL);

				strSQL = String.Format("insert into albuminfo (idAlbumInfo,idAlbum,idArtist,idGenre,strTones,strStyles,strReview,strImage,iRating,iYear,strTracks) values(NULL,{0},{1},{2},'{3}','{4}','{5}','{6}',{7},{8},'{9}' )",
														lAlbumId, lArtistId, lGenreId, 
														album.Tones, 
														album.Styles, 
														album.Review, 
														album.Image, 
														album.Rating, 
														album.Year, 
                            album.Tracks);
				m_db.Execute(strSQL);

				int lAlbumInfoId = m_db.LastInsertID();
				return lAlbumInfoId;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return - 1;

    }
    public void DeleteAlbumInfo(string strAlbumName1)
    {
      string strAlbum = strAlbumName1;
      DatabaseUtility.RemoveInvalidChars(ref strAlbum);
      string strSQL = String.Format("select * from albuminfo,album where albuminfo.idAlbum=album.idAlbum and album.strAlbum like '{0}'",strAlbum);
      SQLiteResultSet results;
      results = m_db.Execute(strSQL);
      if (results.Rows.Count != 0) 
      {
        int iAlbumId = Int32.Parse(DatabaseUtility.Get(results, 0, "albuminfo.idAlbum"));
        strSQL = String.Format("delete from albuminfo where albuminfo.idAlbum={0}",iAlbumId);
        m_db.Execute(strSQL);
      }
    }

    public bool GetAlbumInfo(string strAlbum1, string strPath1, ref AlbumInfo album)
    {
			try
      {
        if (strPath1 == null) return false;
        if (strPath1.Length == 0) return false;
				string strAlbum = strAlbum1;
				string strPath = strPath1;
				//	musicdatabase always stores directories 
				//	without a slash at the end 
				if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
					strPath = strPath.Substring(0, strPath.Length - 1);
				DatabaseUtility.RemoveInvalidChars(ref strAlbum);
				DatabaseUtility.RemoveInvalidChars(ref strPath);
				string strSQL;
				strSQL = String.Format("select * from albuminfo,album,genre,artist where albuminfo.idAlbum=album.idAlbum and albuminfo.idGenre=genre.idGenre and albuminfo.idArtist=artist.idArtist and album.strAlbum like '{0}'",strAlbum);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count != 0) 
				{
					album.Rating = Int32.Parse(DatabaseUtility.Get(results, 0, "albuminfo.iRating"));
					album.Year = Int32.Parse(DatabaseUtility.Get(results, 0, "albuminfo.iYear"));
					album.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
					album.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
					album.Genre = DatabaseUtility.Get(results, 0, "genre.strGenre");
					album.Image = DatabaseUtility.Get(results, 0, "albuminfo.strImage");
					album.Review = DatabaseUtility.Get(results, 0, "albuminfo.strReview");
					album.Styles = DatabaseUtility.Get(results, 0, "albuminfo.strStyles");
          album.Tones = DatabaseUtility.Get(results, 0, "albuminfo.strTones");
          album.Tracks = DatabaseUtility.Get(results, 0, "albuminfo.strTracks");
					//album.Path   = DatabaseUtility.Get(results,0,"path.strPath");
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return false;
    }
	public bool GetAlbumInfo(int albumId, ref AlbumInfo album)
		{
			try
			{
				string strSQL;
				strSQL = String.Format("select * from albuminfo,album,genre,artist where albuminfo.idAlbum=album.idAlbum and albuminfo.idGenre=genre.idGenre and albuminfo.idArtist=artist.idArtist and album.idAlbum ={0}",albumId);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count != 0) 
				{
					album.Rating = Int32.Parse(DatabaseUtility.Get(results, 0, "albuminfo.iRating"));
					album.Year = Int32.Parse(DatabaseUtility.Get(results, 0, "albuminfo.iYear"));
					album.Album = DatabaseUtility.Get(results, 0, "album.strAlbum");
					album.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
					album.Genre = DatabaseUtility.Get(results, 0, "genre.strGenre");
					album.Image = DatabaseUtility.Get(results, 0, "albuminfo.strImage");
					album.Review = DatabaseUtility.Get(results, 0, "albuminfo.strReview");
					album.Styles = DatabaseUtility.Get(results, 0, "albuminfo.strStyles");
					album.Tones = DatabaseUtility.Get(results, 0, "albuminfo.strTones");
					album.Tracks = DatabaseUtility.Get(results, 0, "albuminfo.strTracks");
					//album.Path   = DatabaseUtility.Get(results,0,"path.strPath");
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return false;
		}

    public int  AddArtistInfo(ArtistInfo artist1)
    {
      string strSQL;
      try
      {
        ArtistInfo artist = artist1.Clone();
        string strTmp;
        //strTmp = artist.Artist; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Artist = strTmp;
        strTmp = artist.Born; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Born = strTmp;
        strTmp = artist.YearsActive; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.YearsActive = strTmp;
        strTmp = artist.Genres; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Genres = strTmp;
        strTmp = artist.Instruments; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Instruments = strTmp;
        strTmp = artist.Tones; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Tones = strTmp;
        strTmp = artist.Styles; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Styles = strTmp;
        strTmp = artist.AMGBio; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.AMGBio = strTmp;
        strTmp = artist.Image; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Image = strTmp;
        strTmp = artist.Albums; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Albums = strTmp;
        strTmp = artist.Compilations; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Compilations = strTmp;
        strTmp = artist.Singles; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Singles = strTmp;
        strTmp = artist.Misc; DatabaseUtility.RemoveInvalidChars(ref strTmp); artist.Misc = strTmp;

        if (null == m_db) return - 1;
        int lArtistId = AddArtist(artist.Artist);

        //strSQL=String.Format("delete artistinfo where idArtist={0} ", lArtistId);
        //m_db.Execute(strSQL);
        strSQL = String.Format("select * from artistinfo where idArtist={0}", lArtistId);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count != 0) 
        {
          strSQL = String.Format("delete artistinfo where idArtist={0} ", lArtistId);
          m_db.Execute(strSQL);
        }

        strSQL = String.Format("insert into artistinfo (idArtistInfo,idArtist,strBorn,strYearsActive,strGenres,strTones,strStyles,strInstruments,strImage,strAMGBio, strAlbums,strCompilations,strSingles,strMisc) values(NULL,{0},'{1}','{2}','{3}','{4}','{5}','{6}','{7}','{8}','{9}','{10}','{11}','{12}' )",
          lArtistId, 
          artist.Born, 
          artist.YearsActive, 
          artist.Genres, 
          artist.Tones, 
          artist.Styles, 
          artist.Instruments, 
          artist.Image, 
          artist.AMGBio, 
          artist.Albums, 
          artist.Compilations, 
          artist.Singles, 
          artist.Misc);
        m_db.Execute(strSQL);

        int lArtistInfoId = m_db.LastInsertID();
        return lArtistInfoId;
      }
      catch (Exception ex)
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return - 1;

    }
    public void DeleteArtistInfo(string strArtistName1)
    {
      string strArtist = strArtistName1;
      DatabaseUtility.RemoveInvalidChars(ref strArtist);
      string strSQL = String.Format("select * from artist where artist.strArtist like '{0}'",strArtist);
      SQLiteResultSet results;
      results = m_db.Execute(strSQL);
      if (results.Rows.Count != 0) 
      {
        int iArtistId = Int32.Parse(DatabaseUtility.Get(results, 0, "idArtist"));
        strSQL = String.Format("delete from artistinfo where artistinfo.idArtist={0}",iArtistId);
        m_db.Execute(strSQL);
      }
    }

    public bool GetArtistInfo(string strArtist1,  ref ArtistInfo artist)
    {
      try
      {
        string strArtist = strArtist1;
        DatabaseUtility.RemoveInvalidChars(ref strArtist);
        string strSQL;
        strSQL = String.Format("select * from artist,artistinfo where artist.idArtist=artistinfo.idArtist and artist.strArtist like '{0}'",strArtist);
        SQLiteResultSet results;
        results = m_db.Execute(strSQL);
        if (results.Rows.Count != 0) 
        {
          artist.Artist = DatabaseUtility.Get(results, 0, "artist.strArtist");
          artist.Born = DatabaseUtility.Get(results, 0, "artistinfo.strBorn");
          artist.YearsActive = DatabaseUtility.Get(results, 0, "artistinfo.strYearsActive");
          artist.Genres = DatabaseUtility.Get(results, 0, "artistinfo.strGenres");
          artist.Styles = DatabaseUtility.Get(results, 0, "artistinfo.strStyles");
          artist.Tones = DatabaseUtility.Get(results, 0, "artistinfo.strTones");
          artist.Instruments = DatabaseUtility.Get(results, 0, "artistinfo.strInstruments");
          artist.Image = DatabaseUtility.Get(results, 0, "artistinfo.strImage");
          artist.AMGBio = DatabaseUtility.Get(results, 0, "artistinfo.strAMGBio");
          artist.Albums = DatabaseUtility.Get(results, 0, "artistinfo.strAlbums");
          artist.Compilations = DatabaseUtility.Get(results, 0, "artistinfo.strCompilations");
          artist.Singles = DatabaseUtility.Get(results, 0, "artistinfo.strSingles");
          artist.Misc = DatabaseUtility.Get(results, 0, "artistinfo.strMisc");
          return true;
        }
        return false;
      }
      catch (Exception ex)
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
      }

      return false;
    }

	public bool GetSongsByPath2(string strPath1, ref ArrayList songs)
		{
		Log.Write ("GetSongsByPath2 {0} ",strPath1);
			try
			{
        songs.Clear();
        if (strPath1 == null) return false;
        if (strPath1.Length == 0) return false;
				string strPath = strPath1;
				//	musicdatabase always stores directories 
				//	without a slash at the end 
				if (strPath[strPath.Length - 1] == '/' || strPath[strPath.Length - 1] == '\\')
					strPath = strPath.Substring(0, strPath.Length - 1);
				DatabaseUtility.RemoveInvalidChars(ref strPath);
				if (null == m_db) return false;
				
				string strSQL;
				strSQL = String.Format("select song.idSong,artist.idArtist,album.idAlbum,genre.idGenre,song.favorite,song.strTitle, song.iYear, song.iDuration, song.iTrack, song.iTimesPlayed, song.strFileName,song.iRating, path.strPath, genre.strGenre, album.strAlbum, artist.strArtist from song,path,album,genre,artist where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and path.strPath like '{0}'",strPath);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count == 0) return false;
				for (int i = 0; i < results.Rows.Count; ++i)
				{
					SongMap songmap = new SongMap();
					Song song = new Song();
					song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
					song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
					song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
					song.Track = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTrack"));
					song.Duration = Int32.Parse(DatabaseUtility.Get(results, i, "song.iDuration"));
					song.Year = Int32.Parse(DatabaseUtility.Get(results, i, "song.iYear"));
					song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
					song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed"));
					song.Rating= Int32.Parse(DatabaseUtility.Get(results, i, "song.iRating"));
					song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
					song.songId= Int32.Parse(DatabaseUtility.Get(results, i, "song.idSong"));
					song.artistId= Int32.Parse(DatabaseUtility.Get(results, i, "artist.idArtist"));
					song.albumId= Int32.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"));
					song.genreId= Int32.Parse(DatabaseUtility.Get(results, i, "genre.idGenre"));
					string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
					strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
					song.FileName = strFileName;
					songmap.m_song = song;
					songmap.m_strPath = song.FileName;
					
					songs.Add(songmap);
				}

				return true;
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"GetSongsByPath2: musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return false;
		}

    
    bool		GetSongsByPathes(ArrayList pathes, ref ArrayList songs)
    {
      return false;
    }
	public bool GetSongsByAlbum(string strAlbum1, ref ArrayList songs)
		{
			try
			{
        
				string strAlbum = strAlbum1;
				//	musicdatabase always stores directories 
				//	without a slash at the end 

				DatabaseUtility.RemoveInvalidChars(ref strAlbum);

				songs.Clear();
				if (null == m_db) return false;
    		
				string strSQL;
				strSQL = String.Format("select song.idSong,artist.idArtist,album.idAlbum,genre.idGenre,song.favorite,song.strTitle, song.iYear, song.iDuration, song.iTrack, song.iTimesPlayed, song.strFileName, song.iRating, path.strPath, genre.strGenre, album.strAlbum, artist.strArtist from song,path,album,genre,artist where song.idPath=path.idPath and song.idAlbum=album.idAlbum and song.idGenre=genre.idGenre and song.idArtist=artist.idArtist and album.strAlbum like '{0}' and path.idPath=song.idPath order by song.iTrack", strAlbum);
				SQLiteResultSet results;
				results = m_db.Execute(strSQL);
				if (results.Rows.Count == 0) return false;
				for (int i = 0; i < results.Rows.Count; ++i)
				{
					Song song = new Song();
					song.Artist = DatabaseUtility.Get(results, i, "artist.strArtist");
					song.Album = DatabaseUtility.Get(results, i, "album.strAlbum");
					song.Genre = DatabaseUtility.Get(results, i, "genre.strGenre");
					song.Track = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTrack"));
					song.Duration = Int32.Parse(DatabaseUtility.Get(results, i, "song.iDuration"));
					song.Year = Int32.Parse(DatabaseUtility.Get(results, i, "song.iYear"));
					song.Title = DatabaseUtility.Get(results, i, "song.strTitle");
					song.Favorite=(int)Math.Floor(0.5d+Double.Parse(DatabaseUtility.Get(results, i, "song.favorite")))!=0;
					song.TimesPlayed = Int32.Parse(DatabaseUtility.Get(results, i, "song.iTimesPlayed"));
					song.Rating= Int32.Parse(DatabaseUtility.Get(results, i, "song.iRating"));

					song.artistId= Int32.Parse(DatabaseUtility.Get(results, i, "artist.idArtist"));
					song.albumId= Int32.Parse(DatabaseUtility.Get(results, i, "album.idAlbum"));
					song.genreId= Int32.Parse(DatabaseUtility.Get(results, i, "genre.idGenre"));
					string strFileName = DatabaseUtility.Get(results, i, "path.strPath");
					strFileName += DatabaseUtility.Get(results, i, "song.strFileName");
					song.FileName = strFileName;

					songs.Add(song);
				}

				return true;
			}
			catch (Exception ex) 
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase exception err:{0} stack:{1}", ex.Message, ex.StackTrace);
				Open();
			}

			return false;

		}
    public void CheckVariousArtistsAndCoverArt()
    {
	    if (m_albumCache.Count <= 0)
		    return;

	    foreach (AlbumInfoCache album in m_albumCache)
	    {
		    int lAlbumId = album.idAlbum;
		    int lArtistId = album.idArtist;
		    int lNewArtistId = album.idArtist;
		    bool bVarious = false;
        ArrayList songs = new ArrayList();
		    GetSongsByAlbum(album.Album, ref songs);
		    if (songs.Count > 1)
		    {
			    //	Are the artists of this album all the same
			    for (int i = 0; i < (int)songs.Count - 1; i++)
			    {
				    Song song = (Song)songs[i];
				    Song song1 = (Song)songs[i + 1];
				    if (song.Artist != song1.Artist)
				    {
					    string strVariousArtists = GUILocalizeStrings.Get(340);
					    lNewArtistId = AddArtist(strVariousArtists);
					    bVarious = true;
					    break;
				    }
			    }
		    }

		    if (bVarious)
		    {
			    string strSQL;
			    strSQL = String.Format("update album set idArtist={0} where idAlbum={1}", lNewArtistId, album.idAlbum);
			    m_db.Execute(strSQL);
		    }
/*
		    string strTempCoverArt;
		    string strCoverArt;
		    CUtil::GetAlbumThumb(album.strAlbum+album.strPath, strTempCoverArt, true);
		    //	Was the album art of this album read during scan?
		    if (CUtil::ThumbCached(strTempCoverArt))
		    {
			    //	Yes.
			    //	Copy as permanent directory thumb
			    CUtil::GetAlbumThumb(album.strPath, strCoverArt);
			    ::CopyFile(strTempCoverArt, strCoverArt, false);

			    //	And move as permanent thumb for files and directory, where
			    //	album and path is known
			    CUtil::GetAlbumThumb(album.strAlbum+album.strPath, strCoverArt);
			    ::MoveFileEx(strTempCoverArt, strCoverArt, MOVEFILE_REPLACE_EXISTING);
		    }*/
	    }

	    m_albumCache.Clear();
    }

    public void BeginTransaction()
    {
      try
      {
        m_db.Execute("begin");
      }
      catch (Exception ex)
      {
				Log.WriteFile(Log.LogType.Log,true,"BeginTransaction: musicdatabase begin transaction failed exception err:{0} ", ex.Message);
				//Open();
      }
    }
    
    public void CommitTransaction()
    {
      Log.Write ("Commit will effect {0} rows",m_db.ChangedRows());
	  SQLiteResultSet CommitResults;
		if (m_db.ChangedRows() == 0) 
		{
			Log.Write ("MusicDatabase: Commit not necessary, there are no changes.");
		}
		else
		{
			try
			{
				CommitResults = m_db.Execute("commit");
			}
			catch (Exception ex)
			{
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase commit failed exception err:{0} ", ex.Message);
				Open();
			}
		}
    }
    
    public void RollbackTransaction()
    {
      try
      {
        m_db.Execute("rollback");
      }
      catch (Exception ex)
      {
				Log.WriteFile(Log.LogType.Log,true,"musicdatabase rollback failed exception err:{0} ", ex.Message);
				Open();
      }
    }


	#region		DatabaseReBuild
      SQLiteResultSet PathResults;
	  SQLiteResultSet PathDeleteResults;
	  int NumPaths;
      int PathNum;
	  string strSQL;


	  public int MusicDatabaseReorg ()
	  {
		/// Tfro71 29-5-2005
		/// Added this method so that we can use it in front-end and in configuration
		/// and to prevent double code for the same thing.
		/// 
		  DatabaseReorgEventArgs MyArgs =new DatabaseReorgEventArgs(); 

	  
		/// Delete song that are in non-existing MusicFolders (for example: you moved everything to another disk)
		  MyArgs.progress=2;
		  MyArgs.phase="Removing songs in old folders";
		  OnDatabaseReorgChanged(MyArgs);
		  DeleteSongsOldMusicFolders();

		  
		  /// Delete files that don't exist anymore (example: you deleted files from the Windows Explorer)
		  /// MyArgs.progress=10;
		  MyArgs.progress=4;
		  MyArgs.phase="Removing non existing songs";
		  OnDatabaseReorgChanged(MyArgs);
  		  DeleteNonExistingSongs();

	      /// Add missing files (example: You downloaded some new files)
		  MyArgs.progress=6;
		  MyArgs.phase="Adding new files";
		  OnDatabaseReorgChanged(MyArgs);

		  int AddMissingFilesResult= AddMissingFiles(8,50);
		  CommitTransaction();
		  Log.Write ("Musicdatabasereorg: Addmissingfiles: {0} files added",AddMissingFilesResult);

		  /// Update the tags
		  MyArgs.progress=50;
		  MyArgs.phase="Updating tags";
		  OnDatabaseReorgChanged(MyArgs);
		  UpdateTags(52,88);	//This one works for all the files in the MusicDatabase
		  CommitTransaction();

		  /// Cleanup foreign keys tables.
		  /// We added, deleted new files
		  /// We update all the tags
		  /// Now lets clean up all the foreign keus
		  MyArgs.progress=90;
		  MyArgs.phase="Checking Artists";
		  OnDatabaseReorgChanged(MyArgs);
		  ExamineAndDeleteArtistids();

		  MyArgs.progress=92;
		  MyArgs.phase="Checking Genres";
		  OnDatabaseReorgChanged(MyArgs);
		  ExamineAndDeleteGenreids();

		  MyArgs.progress=94;
		  MyArgs.phase="Checking Paths";
		  OnDatabaseReorgChanged(MyArgs);
		  ExamineAndDeletePathids();

		  MyArgs.progress=96;
		  MyArgs.phase="Checking Albums";
		  OnDatabaseReorgChanged(MyArgs);
		  ExamineAndDeleteAlbumids();
		  
		  MyArgs.progress=98;
		  MyArgs.phase="Compressing the database";
		  OnDatabaseReorgChanged(MyArgs);
		  Compress();

		  CommitTransaction();

		  MyArgs.progress=100;
		  MyArgs.phase="Finished";
		  OnDatabaseReorgChanged(MyArgs);

        EmptyCache();
		return (int)Errors.ERROR_OK;
	}

	  int  UpdateTags(int StartProgress, int EndProgress)
	  {
		  string strSQL;
		  int NumRecodsUpdated=0;
		  Log.Write("Musicdatabasereorg: starting Tag update");
		
		  SQLiteResultSet FileList;
		  strSQL=String.Format("select * from song, path where song.idPath=path.idPath");
			
		  try
		  {
			  FileList = m_db.Execute(strSQL);
			  if (FileList==null) 
			  {
				  Log.Write ("Musicdatabasereorg: UpdateTags: Select from failed");
				  return (int)Errors.ERROR_REORG_SONGS;
			  }
		  }
		  catch (Exception)
		  {
			  Log.Write("Musicdatabasereorg: query for tag update could not be executed.");
			  //m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_SONGS;
		  }

		  //	songs cleanup

		  Log.Write ("Going to check tags of {0} files",FileList.Rows.Count);

		  DatabaseReorgEventArgs MyArgs =new DatabaseReorgEventArgs(); 
		  int ProgressRange=EndProgress-StartProgress;
		  int TotalSongs = FileList.Rows.Count;
		  int SongCounter=0;

		  double NewProgress;

		  for (int i=0; i < FileList.Rows.Count;++i)
		  {
			  string strFileName = DatabaseUtility.Get(FileList,i,"path.strPath") ;
			  strFileName += DatabaseUtility.Get(FileList,i,"song.strFileName") ;
			  //Log.Write("Musicdatabasereorg: starting Tag update 1 for {0} ", strFileName);

			  if (System.IO.File.Exists(strFileName))
			  {
				  /// PDW 24-MAY-2005
				  /// Added description
				  /// The file for this song still exists so we can update the Tags
				  int idAlbumNew=0;
				  int idArtistNew=0;
				  int idPathNew=0;
				  int idGenreNew=0;

				  //Log.Write("Musicdatabasereorg: starting Tag update 2 for existing file {0} ", strFileName);
				  int idSong=Int32.Parse( DatabaseUtility.Get(FileList,i,"song.idSong"));

				  //Log.Write("Musicdatabasereorg: starting Tag update 3 for existing file {0} ", strFileName);
				  try
				  {
					  int idAlbum=idAlbumNew=Int32.Parse( DatabaseUtility.Get(FileList,i,"song.idAlbum"));
				  }
				  catch (Exception )
				  {
					  Log.Write("Musicdatabasereorg: failed Tag update 3 for existing file {0} ", strFileName);
				  }

				  //Log.Write("Musicdatabasereorg: starting Tag update 4 for existing file {0} ", strFileName);
				  try
				  {
					  int idArtist=idArtistNew=Int32.Parse( DatabaseUtility.Get(FileList,i,"song.idArtist"));
				  }
				  catch (Exception )
				  {
					  Log.Write("Musicdatabasereorg: failed Tag update 4 for existing file {0} ", strFileName);
				  }
				  
				  //Log.Write("Musicdatabasereorg: starting Tag update 5 for existing file {0} ", strFileName);
				  try
				  {
					  int idPath=idPathNew=Int32.Parse( DatabaseUtility.Get(FileList,i,"song.idPath"));
				  }
				  catch (Exception )
				  {
					  Log.Write("Musicdatabasereorg: failed Tag update 5 for existing file {0} ", strFileName);
				  }

				  
				  //Log.Write("Musicdatabasereorg: starting Tag update 6 for existing file {0} ", strFileName);
				  try
				  {
					  int idGenre=idGenreNew=Int32.Parse( DatabaseUtility.Get(FileList,i,"song.idGenre"));
				  }
				  catch (Exception )
				  {
					  Log.Write("Musicdatabasereorg: failed Tag update 7 for existing file {0} ", strFileName);
				  }


				  /// PDW 24-MAY-2005
				  /// The song will be updated, tags from the file will be checked against the tags in the database
				  /// But why do we send all the id's
				  //Log.Write("Musicdatabasereorg: starting Tag update 7 for {0} ", strFileName);
				  if (!UpdateSong(strFileName, idSong, ref idAlbumNew, ref idArtistNew, ref idGenreNew, ref idPathNew))
				  {
					  Log.Write("Musicdatabasereorg: Song update after tag update failed for", strFileName);
					  //m_db.Execute("rollback"); 
					  return (int)Errors.ERROR_REORG_SONGS;
				  }
				  else
				  {
					  NumRecodsUpdated=NumRecodsUpdated+1;
				  }
			  }
			  NewProgress = StartProgress+((ProgressRange*SongCounter)/TotalSongs);
			  MyArgs.progress =Convert.ToInt32(NewProgress);
			  MyArgs.phase="Updating tags";
			  OnDatabaseReorgChanged(MyArgs);
			  SongCounter++;
		  }//for (int i=0; i < results.Rows.Count;++i)
		  Log.Write ("Musicdatabasereorg: UpdateTags completed for {0} songs",(int)NumRecodsUpdated);
		  return (int)Errors.ERROR_OK;
	  }

      bool UpdateSong(string  strPathSong, int idSong, ref int idAlbum, ref int idArtist, ref int idGenre, ref int idPath)
	  {
		  MusicTag tag;
		  tag=TagReader.TagReader.ReadTag(strPathSong);									
		  if (tag!=null)
		  {
			  //Log.Write ("Musicdatabasereorg: We are gonna update the tags for {0}", strPathSong);
			  Song song		= new Song();
			  song.Title		= tag.Title;
			  song.Genre		= tag.Genre;
			  song.FileName	= strPathSong;
			  song.Artist		= tag.Artist;
			  song.Album		= tag.Album;
			  song.Year		= tag.Year;
			  song.Track		= tag.Track;
			  song.Duration	= tag.Duration;

			  string strPath, strFileName;
			  DatabaseUtility.Split(song.FileName, out strPath, out strFileName); 

			  string strTmp;
			  strTmp=song.Album;DatabaseUtility.RemoveInvalidChars(ref strTmp);song.Album=strTmp;
			  strTmp=song.Genre;DatabaseUtility.RemoveInvalidChars(ref strTmp);song.Genre=strTmp;
			  strTmp=song.Artist;DatabaseUtility.RemoveInvalidChars(ref strTmp);song.Artist=strTmp;
			  strTmp=song.Title;DatabaseUtility.RemoveInvalidChars(ref strTmp);song.Title=strTmp;

			  DatabaseUtility.RemoveInvalidChars(ref strFileName);

			  /// PDW 25 may 2005
			  /// Adding these items starts a select and insert query for each. 
			  /// Maybe we should check if anything has changed in the tags
			  /// if not, no need to add and invoke query's.
			  /// here we are gonna (try to) add the tags
				
			  idGenre  = AddGenre(tag.Genre);
			  //Log.Write ("Tag.genre = {0}",tag.Genre);
			  idArtist = AddArtist(tag.Artist);
			  //Log.Write ("Tag.Artist = {0}",tag.Artist);
			  idPath   = AddPath(strPath);
			  //Log.Write ("strPath= {0}",strPath);
			  idAlbum  = AddAlbum(tag.Album,idArtist);
			  //Log.Write ("Tag.Album = {0}",tag.Album);

			  ulong dwCRC=0;
			  CRCTool crc= new CRCTool();
			  crc.Init(CRCTool.CRCCode.CRC32);
			  dwCRC=crc.calc(strFileName);
			  //SQLiteResultSet results;

			  Log.Write ("Song {0} will be updated with CRD={1}",song.FileName,dwCRC);

			  string strSQL;
			  strSQL=String.Format("update song set idArtist={0},idAlbum={1},idGenre={2},idPath={3},strTitle='{4}',iTrack={5},iDuration={6},iYear={7},dwFileNameCRC='{8}',strFileName='{9}' where idSong={10}",
				  idArtist,idAlbum,idGenre,idPath,
				  song.Title,
				  song.Track,song.Duration,song.Year,
				  dwCRC,
				  strFileName, idSong);
			  //Log.Write (strSQL);
			  try
			  {
				  m_db.Execute(strSQL);
			  }
			  catch(Exception)
			  {
				  Log.Write ("Musicdatabasereorg: Update tags for {0} failed because of DB exception", strPathSong);
				  return false;
			  }
		  }
		  else
		  {
			  Log.Write ("Musicdatabasereorg: Update for {0} failed because of a NULL tag", strPathSong);
		  }
		  //Log.Write ("Musicdatabasereorg: Update for {0} success", strPathSong);
		  return true;
	  }


	  int  DeleteNonExistingSongs()
	  {
		  string strSQL;
			  /// Opening the MusicDatabase
		  try
		  {
			  MusicDatabase.DBHandle.Execute("begin"); 
		  }
		  catch (Exception )
		  {
			  return (int)Errors.ERROR_DATABASE;
		  }
		  SQLiteResultSet results;
		  strSQL=String.Format("select * from song, path where song.idPath=path.idPath");
		  try
		  {
			  results = MusicDatabase.DBHandle.Execute(strSQL);
			  if (results==null) return (int)Errors.ERROR_REORG_SONGS;
		  }
		  catch (Exception)
		  {
			  //MusicDatabase.DBHandle.Execute("rollback");
			  return (int)Errors.ERROR_REORG_SONGS;
		  }
		  Log.Write("Musicdatabasereorg: starting song cleanup for {0} songs", (int) results.Rows.Count );
		  for (int i=0; i < results.Rows.Count;++i)
		  {
			  string strFileName = DatabaseUtility.Get(results,i,"path.strPath") ;
			  strFileName += DatabaseUtility.Get(results,i,"song.strFileName") ;
			  ///pDlgProgress.SetLine(2, System.IO.Path.GetFileName(strFileName) );
	
			  if (! System.IO.File.Exists(strFileName))
			  {
				  /// song doesn't exist anymore, delete it
				  /// We don't care about foreign keys at this moment. We'll just change this later.
					
				  Log.Write("Musicdatabasereorg:Song {0} will to be deleted from MusicDatabase", strFileName);
				  DeleteSong (strFileName,false);

			  }
		  }//for (int i=0; i < results.Rows.Count;++i)
		  Log.Write ("Musicdatabasereorg: DeleteNonExistingSongs completed");
		  return (int)Errors.ERROR_OK;
	  }
    
	  int  ExamineAndDeleteArtistids()
	  {
		  /// This will delete all artists and artistinfo from the database that don't have a corresponding song anymore
		  /// First delete all the albuminfo before we delete albums (foreign keys)

		  /// TODO: delete artistinfo first
		  string strSql="delete from artist where artist.idArtist not in (select idArtist from song);" ;
		  try
		  {
			  m_db.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: ExamineAndDeleteArtistids failed");
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_ARTIST;
		  }

		  Log.Write ("Musicdatabasereorg: ExamineAndDeleteArtistids completed");
		  return (int)Errors.ERROR_OK;
	  }

	  int  ExamineAndDeleteGenreids()
	  {
		  /// This will delete all genres from the database that don't have a corresponding song anymore
		  SQLiteResultSet result;
		  string strSql="delete from genre where idGenre not in (select idGenre from song);" ;
		  try
		  {
			  m_db.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: ExamineAndDeleteGenreids failed");
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_GENRE;
		  }

		  strSql="select count (*) aantal from genre where idGenre not in (select idGenre from song);" ;
		  try
		  {
			  result=MusicDatabase.DBHandle.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: ExamineAndDeleteGenreids failed");
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_GENRE;

		  }
		  string Aantal = DatabaseUtility.Get(result,0,"aantal") ;
		  if (Aantal != "0" )
			  return (int)Errors.ERROR_REORG_GENRE;
		  Log.Write ("Musicdatabasereorg: ExamineAndDeleteGenreids completed");

		  return (int)Errors.ERROR_OK;
	  }

	  int  ExamineAndDeletePathids()
	  {
		/// This will delete all paths from the database that don't have a corresponding song anymore
		string strSql=String.Format("delete from path where idPath not in (select idPath from song)" );
		  try
		  {
			  m_db.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: ExamineAndDeletePathids failed");
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_PATH;
		  }
		  Log.Write ("Musicdatabasereorg: ExamineAndDeletePathids completed");
		  return (int)Errors.ERROR_OK;
	  }

	  int  ExamineAndDeleteAlbumids()
	  {
		  /// This will delete all albums from the database that don't have a corresponding song anymore
		  /// First delete all the albuminfo before we delete albums (foreign keys)
		  string strSql=String.Format("delete from albuminfo where idAlbum not in (select idAlbum from song)" );
		  try
		  {
			  m_db.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_ALBUM;
		  }
		  /// Now all the albums without songs will be deleted.
		  ///SQLiteResultSet results;
		  strSql=String.Format("delete from album where idAlbum not in (select idAlbum from song)" );
		  try
		  {
			  m_db.Execute(strSql); 
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: ExamineAndDeleteAlbumids failed");
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_ALBUM;
		  }
		  Log.Write ("Musicdatabasereorg: ExamineAndDeleteAlbumids completed");
		  return (int)Errors.ERROR_OK;
	  }
	  int  Compress()
	  {
		  //	compress database
		  try
		  {
			  MusicDatabase.DBHandle.Execute("vacuum");
		  }
		  catch(Exception)
		  {
			  return (int)Errors.ERROR_COMPRESSING;
		  }
		  Log.Write ("Musicdatabasereorg: Compress completed");
		  return (int)Errors.ERROR_OK;
	  }

	  int  LoadShares()
	  {
		  /// 25-may-2005 TFRO71
		  /// Added this function to make scan the Music Shares that are in the configuration file.
		  /// Songs that are not in these Shares will be removed from the MusicDatabase
		  /// The files will offcourse not be touched
		  string	currentFolder = String.Empty;
		  bool											fileMenuEnabled=false;
		  string										fileMenuPinCode=String.Empty;
			
		  using (MediaPortal.Profile.Xml xmlreader = new MediaPortal.Profile.Xml("MediaPortal.xml"))
		  {
			  fileMenuEnabled = xmlreader.GetValueAsBool("filemenu", "enabled", true);
				
			  string strDefault = xmlreader.GetValueAsString("music", "default",String.Empty);
			  for (int i = 0; i < 20; i++)
			  {
				  string strShareName = String.Format("sharename{0}",i);
				  string strSharePath = String.Format("sharepath{0}",i);
					
				  string shareType = String.Format("sharetype{0}", i);
				  string shareServer = String.Format("shareserver{0}", i);
				  string shareLogin = String.Format("sharelogin{0}", i);
				  string sharePwd  = String.Format("sharepassword{0}", i);
				  string sharePort = String.Format("shareport{0}", i);
				  string remoteFolder = String.Format("shareremotepath{0}", i);

				  string SharePath = xmlreader.GetValueAsString("music", strSharePath, String.Empty);
					
				  if (SharePath.Length>0) 
					  m_shares.Add(SharePath);
			  }
		  }
		  return 0;
	  }

	  int  DeleteSongsOldMusicFolders()
	  {

		  /// PDW 24-05-2005
		  /// Here we handle the songs in non-existing MusicFolders (shares).
		  /// So we have to check Mediaportal.XML
		  /// Loading the current MusicFolders
		  Log.Write("Musicdatabasereorg: deleting songs in non-existing shares");
		  int result = LoadShares();

		  /// For each path in the MusicDatabase we will check if it's in a share
		  /// If not, we will delete all the songs in this path.
		  strSQL=String.Format("select * from path");
		
		  try
		  {
			  PathResults = MusicDatabase.DBHandle.Execute(strSQL);
			  if (PathResults==null) return (int)Errors.ERROR_REORG_SONGS;
		  }
		  catch (Exception)
		  {
			  MusicDatabase.DBHandle.Execute("rollback");
			  return (int)Errors.ERROR_REORG_SONGS;
		  }
		  NumPaths = PathResults.Rows.Count;
			
		  /// We will walk through all the paths (from the songs) and see if they match with a share/MusicFolder (from the config)
		  for ( PathNum=0; PathNum < PathResults.Rows.Count;++PathNum)
		  {
			  string Path= DatabaseUtility.Get(PathResults,PathNum,"strPath") ;
			  string PathId= DatabaseUtility.Get(PathResults,PathNum,"idPath") ;
			  /// We now have a path, we will check it along all the shares
			  bool Path_has_Share = false;
			  foreach (string Share in m_shares)
			  {
				  ///Here we can check if the Path has an existing share
				  string Path_part =Path.Substring(0,Share.Length);
				  if (Share.ToUpper() == Path_part.ToUpper() )
					  Path_has_Share = true;
			  }
			  if (!Path_has_Share)
			  {
				  Log.Write ("Musicdatabasereorg: Path {0} with id {1} has no corresponding share, songs will be deleted ",Path,PathId);
				  strSQL=String.Format("delete from song where idPath = {0}",PathId);
				  try
				  {
					  PathDeleteResults = MusicDatabase.DBHandle.Execute(strSQL);
					  if (PathDeleteResults==null) return (int)Errors.ERROR_REORG_SONGS;
				  }
				  catch (Exception)
				  {
					  MusicDatabase.DBHandle.Execute("rollback");
					  return (int)Errors.ERROR_REORG_SONGS;
				  }

				  Log.Write ("Trying to commit the deletes from the DB");
				  /// This still gives some stupid errors. Doing this twice in MP does the track but
				  /// it crashes the first. WTF!
				  CommitTransaction();
			  } /// If path has no share
		  } /// For each path
	    Log.Write ("Musicdatabasereorg: DeleteSongsOldMusicFolders completed");
		return 	(int) Errors.ERROR_OK;
	  } // DeleteSongsOldMusicFolders

	  #endregion

	  private string[] Extensions
	  {
		  get { return extensions; }
		  set { extensions = value; }
	  }
	  string[] extensions = new string[] { ".mp3" };
	  
	  /// Todo: add other filetypes because only mp3 is not enough
	  /// WMV if with video (haha, that might me awsome!)
	  
	  ArrayList availableFiles;


	  private int AddMissingFiles(int StartProgress, int EndProgress)
	  {
		  m_shares.Clear();
		  /// This seems to clear the arraylist and make it valid
		  availableFiles = new ArrayList();
		  Log.Write ("13");

		  DatabaseReorgEventArgs MyArgs =new DatabaseReorgEventArgs(); 
		  string strSQL;
		  ulong dwCRC;
		  CRCTool crc = new CRCTool();
		  crc.Init(CRCTool.CRCCode.CRC32);

		  int totalFiles=0;

		  int ProgressRange=EndProgress-StartProgress;
		  int TotalSongs;
		  int SongCounter=0;
		  int AddedCounter=0;
		  string MusicFilePath, MusicFileName;
		  double NewProgress;

		  LoadShares();
		  foreach (string Share in m_shares)
		  {
			  ///Here we can check if the Path has an existing share
			  CountFilesInPath (Share, ref totalFiles);
		  }
		  TotalSongs = totalFiles;
		  Log.Write("Musicdatabasereorg: Found {0} files to check if they are new", (int)totalFiles );
		  SQLiteResultSet results;

		  foreach (string MusicFile in availableFiles)
		  {
			  ///Here we can check if the Path has an existing share
			  ///
			  SongCounter++;
			  DatabaseUtility.Split(MusicFile, out MusicFilePath, out MusicFileName);

			  dwCRC = crc.calc(MusicFileName);

			  /// Convert.ToChar(34) wil give you a "
			  /// This is handy in building strings for SQL
			  strSQL = String.Format("select * from song,path where song.idPath=path.idPath and strFileName={1}{0}{1} and strPath={1}{2}{1}", MusicFileName, Convert.ToChar(34), MusicFilePath);
			  //Log.Write (strSQL);
			  //Log.Write (MusicFilePath);
			  //Log.Write (MusicFile);
			  //Log.Write (MusicFileName);

			  try
			  {
				  results = m_db.Execute(strSQL);
				  if (results==null) 
				  {	
					  Log.Write ("Musicdatabasereorg: AddMissingFiles finished with error (results == null)");
					  return (int)Errors.ERROR_REORG_SONGS;
				  }
			  }
			  catch (Exception)
			  {
				  Log.Write ("Musicdatabasereorg: AddMissingFiles finished with error (exception for select)");
				  m_db.Execute("rollback");
				  return (int)Errors.ERROR_REORG_SONGS;
			  }
			  
			  if (results.Rows.Count>=1)
			  {
				  /// The song exists
				  /// Log.Write ("Song {0} exists, dont do a thing",MusicFileName);
				  /// string strFileName = DatabaseUtility.Get(results,0,"path.strPath") ;
				  /// strFileName += DatabaseUtility.Get(results,0,"song.strFileName") ;
			  }
			  else
			  {
				  //The song does not exist, we will add it.
				  AddSong(MusicFileName,MusicFilePath);
				  AddedCounter++;
			  }
			  NewProgress = StartProgress+((ProgressRange*SongCounter)/TotalSongs);
			  MyArgs.progress =Convert.ToInt32(NewProgress);
			  MyArgs.phase="Checking for new files";
			  OnDatabaseReorgChanged(MyArgs);
		  } //end for-each
		  Log.Write ("Musicdatabasereorg: AddMissingFiles finished with SongCounter = {0}",SongCounter);
		  Log.Write ("Musicdatabasereorg: AddMissingFiles finished with AddedCounter = {0}",AddedCounter);
		  return SongCounter;
	  }

	  /// <summary>
	  /// TFRO 7 june 2005
	  /// This is the method that adds songs, you need to check existence of the file before. This method
	  /// will just add it.
	  /// </summary>
	  /// <param name="MusicFileName"></param>
	  /// <param name="MusicFilePath"></param>
	  /// <returns></returns>

	  private int AddSong (string MusicFileName, string MusicFilePath)
	  {
		  SQLiteResultSet results;
		  
		  int idPath= AddPath(MusicFilePath);
		  int idArtist = AddArtist ("unknown");
		  int idAlbum = AddAlbum ("unknown",idArtist);
		  int idGenre = AddGenre ("unknown");

		  /// Here we are gonna make a CRC code to add to the database
		  /// This coded is used for searching on the filename
		  ulong dwCRC=0;
		  CRCTool crc= new CRCTool();
		  crc.Init(CRCTool.CRCCode.CRC32);
		  dwCRC=crc.calc(MusicFileName);

		  Log.Write ("Song {0} will be added with CRC {1}",MusicFileName,dwCRC);
		  /// Here we add song to the database
		  strSQL = String.Format("insert into song (idPath,strFileName,idAlbum,idArtist,idGenre,dwFileNameCRC,iTimesPlayed,iRating,favorite) values ({0},{2}{1}{2},{3},{4},{5},{6},{7},{8},{9})",idPath, MusicFileName, Convert.ToChar(34),idAlbum,idArtist,idGenre,dwCRC,0,0,0);
		  //Log.Write (strSQL);
		  try
		  {
			  Log.Write ("Musicdatabasereorg: Insert {0}{1} into the database",MusicFilePath,MusicFileName);
			  results = m_db.Execute(strSQL);
			  if (results==null) 
			  {
				  Log.Write ("Musicdatabasereorg: Insert of song {0}{1} failed",MusicFilePath,MusicFileName);
				  return (int)Errors.ERROR_REORG_SONGS;
			  }
		  }
		  catch (Exception)
		  {
			  Log.Write ("Musicdatabasereorg: Insert of song {0}{1} failed",MusicFilePath,MusicFileName);
			  m_db.Execute("rollback");
			  return (int)Errors.ERROR_REORG_SONGS;
		  }

		  //Log.Write ("Musicdatabasereorg: Insert of song {0}{1} success",MusicFilePath,MusicFileName);
		  return (int)Errors.ERROR_OK;
	  }

	  private void CountFilesInPath(string path, ref int totalFiles)
	  {
		  //
		  // Count the files in the current directory
		  //
		  //Log.Write("Musicdatabasereorg: Counting files in {0}", path );

		  try
		  {
			  foreach(string extension in Extensions)
			  {
				  string[] files = Directory.GetFiles(path, String.Format("*{0}", extension));
				  availableFiles.AddRange(files);
				  totalFiles += files.Length;
			  }
		  }
		  catch
		  {
			  // Ignore
		  }

		  //
		  // Count files in subdirectories
		  //
		  try
		  {
			  string[] directories = Directory.GetDirectories(path);

			  foreach(string directory in directories)
			  {
				  CountFilesInPath(directory, ref totalFiles);
			  }
		  }
		  catch
		  {
			  // Ignore
		  }
	  }


	    }
}
