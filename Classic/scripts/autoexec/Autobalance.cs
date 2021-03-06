// Team Autobalance Script
//
// Determines which team needs players and proceeds to switch them
// Goon style: At respawn
//
// Enable or Disable Autobalance
// $Host::EnableAutobalance = 1;
//
// exec("scripts/autoexec/Autobalance.cs");
//
// If it takes too long for specific canidates to die. After a time choose anyone.
$Autobalance::Fallback = 120000; //60000 is 1 minute

// Run from TeamBalanceNotify.cs via NotifyUnbalanced
function Autobalance( %game )
{
	if(isEventPending($AutoBalanceSchedule))
		cancel($AutoBalanceSchedule);

	if($TBNStatus !$= "NOTIFY") //If Status has changed to EVEN or anything else (GameOver reset).
		return;

	//Difference Variables
	%team1difference = $TeamRank[1, count] - $TeamRank[2, count];
	%team2difference = $TeamRank[2, count] - $TeamRank[1, count];

	//Determine BigTeam
	if( %team1difference >= 2 )
		$BigTeam = 1;
	else if( %team2difference >= 2 )
		$BigTeam = 2;
	else
		return;

	$Autobalance::UseAllMode = 0;
	$Autobalance::FallbackTime = getSimTime();
	%otherTeam = $BigTeam == 1 ? 2 : 1;
	$Autobalance::AMThreshold = mCeil(MissionGroup.CTF_scoreLimit/3) * 100;

	//If BigTeam score is greater than otherteam score + threshold
	if($TeamScore[$BigTeam] > ($TeamScore[%otherTeam] + $Autobalance::AMThreshold) || $TeamRank[%otherTeam, count] $= 0)
		$Autobalance::UseAllMode = 1;
	//If BigTeam Top Players score is greater than otherTeam Top Players score + threshold
	else if($TeamRank[$BigTeam, count] >= 5 && $TeamRank[%otherTeam, count] >= 3)
	{
		%max = mfloor($TeamRank[$BigTeam, count]/2);
		if(%max > $TeamRank[%otherTeam, count])
			%max = $TeamRank[%otherTeam, count];
		%threshold = %max * 100;
		for(%i = 0; %i < %max; %i++)
		{
			%bigTeamTop = %bigTeamTop + $TeamRank[$BigTeam, %i].score;
			%otherTeamTop = %otherTeamTop + $TeamRank[%otherTeam, %i].score;
		}

		if(%bigTeamTop > (%otherTeamTop + %threshold))
			$Autobalance::UseAllMode = 1;
	}
	//echo("Allmode " @  $Autobalance::UseAllMode);

	//Select lower half of team rank as canidates for team change
	if(!$Autobalance::UseAllMode)
	{
		//Reset clients canidate var
		ResetABClients();

		$Autobalance::Max = mFloor($TeamRank[$BigTeam, count]/2);
		for(%i = $Autobalance::Max; %i < $TeamRank[$BigTeam, count]; %i++)
		{
			//echo("[Autobalance]: Selected" SPC $TeamRank[$BigTeam, %i].nameBase @ ", " @ %i);
			$TeamRank[$BigTeam, %i].abCanidate = true;
		}
		%a = " selected";
	}

	if($TeamRank[$BigTeam, count] - $TeamRank[%otherTeam, count] >= 3)
		%s = "s";

	//Warning message
	messageAll('MsgTeamBalanceNotify', '\c1Teams are unbalanced: \c0Autobalance will switch the next%3 respawning player%2 on Team %1.', $TeamName[$BigTeam], %s, %a);
}

function ResetABClients()
{
	for(%i = 0; %i < $TeamRank[$BigTeam, count]; %i++)
	{
		$TeamRank[$BigTeam, %i].abCanidate = false;
	}
}

package Autobalance
{

function DefaultGame::onClientKilled(%game, %clVictim, %clKiller, %damageType, %implement, %damageLocation)
{
	parent::onClientKilled(%game, %clVictim, %clKiller, %damageType, %implement, %damageLocation);

	if($BigTeam !$= "" && %clVictim.team == $BigTeam)
	{
		%otherTeam = $BigTeam == 1 ? 2 : 1;
		if($TeamRank[$BigTeam, count] - $TeamRank[%otherTeam, count] >= 2)
		{
			%fallback = 0;
			if((getSimTime() - $Autobalance::FallbackTime) > $Autobalance::Fallback)
				%fallback = 1;

			//damageType 0: If someone switches to observer or disconnects
			if(%damageType !$= 0 && (%clVictim.abCanidate || $Autobalance::UseAllMode || %fallback))
			{
				echo("[Autobalance]" SPC %clVictim.nameBase @ " has been moved to Team " @ %otherTeam @ " for balancing. [AM:" @ $Autobalance::UseAllMode SPC "#BT:" @ ($TeamRank[$BigTeam, count]-1) SPC "#OT:" @ ($TeamRank[%otherTeam, count]+1) SPC "FB:" @ %fallback @ "]");
				messageClient(%clVictim, 'MsgTeamBalanceNotify', '\c0You were switched to Team %1 for balancing.~wfx/powered/vehicle_screen_on.wav', $TeamName[%otherTeam]);
				messageAllExcept(%clVictim, -1, 'MsgTeamBalanceNotify', '~wfx/powered/vehicle_screen_on.wav');

				Game.clientChangeTeam( %clVictim, %otherTeam, 0 );
			}
		}
		else
		{
			ResetABClients();
			ResetTBNStatus();
			$BigTeam = "";
		}
	}
}

function DefaultGame::gameOver(%game)
{
	Parent::gameOver(%game);

	//Reset Autobalance
	$BigTeam = "";

	//Reset all clients canidate var
	for (%i = 0; %i < ClientGroup.getCount(); %i++)
	{
		%client = ClientGroup.getObject(%i);
		%client.abCanidate = false;
	}
}

};

// Prevent package from being activated if it is already
if (!isActivePackage(Autobalance))
	activatePackage(Autobalance);
