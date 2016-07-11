#region Copyright (C) 2005-2010 Team MediaPortal

// Copyright (C) 2005-2010 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.ComponentModel;

namespace Mediaportal.TV.Server.Common.Types.Enum
{
  /// <summary>
  /// Encode modes.
  /// </summary>
  [Flags]
  public enum EncodeMode
  {
    /// <summary>
    /// Default mode.
    /// </summary>
    Default = 0,
    /// <summary>
    /// Constant bit-rate mode.
    /// </summary>
    [Description("Constant")]
    ConstantBitRate = 1,
    /// <summary>
    /// Variable bit-rate mode.
    /// </summary>
    [Description("Variable")]
    VariableBitRate = 2,
    /// <summary>
    /// Variable peak bit-rate mode.
    /// </summary>
    [Description("Variable Peak")]
    VariablePeakBitRate = 4
  }
}